using System;
using System.Collections.Generic;
using Resources.PathCreator.Core.Editor.Helper;
using Resources.PathCreator.Core.Runtime.Objects;
using Resources.PathCreator.Core.Runtime.Utility;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Debug = System.Diagnostics.Debug;

namespace Resources.PathCreator.Core.Editor
{
	/// Editor class for the creation of Bezier and Vertex paths

	[CustomEditor(typeof(Runtime.Objects.PathCreator))]
	public class PathEditor : UnityEditor.Editor
	{
		#region Fields

		// Interaction:
		private const float SegmentSelectDistanceThreshold = 10.0f;
		private const float ScreenPolylineMaxAngleError = 0.3f;
		private const float ScreenPolylineMinVertexDst = 0.01f;

		// Help messages:
		private const string HelpInfo = "Shift-click to add or insert new points. Control-click to delete points. For more detailed infomation, please refer to the documentation.";
		private static readonly string[] SpaceNames = { "3D (xyz)", "2D (xy)", "Top-down (xz)" };
		private static readonly string[] TabNames = { "Bézier Path", "Vertex Path" };
		private const string ConstantSizeTooltip = "If true, anchor and control points will keep a constant size when zooming in the editor.";

		// Display
		private const int InspectorSectionSpacing = 10;
		private const float ConstantHandleScale = 0.01f;
		private const float NormalsSpacing = 0.2f;
		private GUIStyle _boldFoldoutStyle;

		// References:
		private Runtime.Objects.PathCreator _creator;
		private UnityEditor.Editor _globalDisplaySettingsEditor;
		private ScreenSpacePolyLine _screenSpaceLine;
		private ScreenSpacePolyLine.MouseInfo _pathMouseInfo;
		private GlobalDisplaySettings _globalDisplaySettings;
		private PathHandle.HandleColours _splineAnchorColours;
		private PathHandle.HandleColours _splineControlColours;
		private Dictionary<GlobalDisplaySettings.HandleType, Handles.CapFunction> _capFunctions;
		private readonly ArcHandle _anchorAngleHandle = new ArcHandle();
		private VertexPath _normalsVertexPath;
		
		// State variables:
		private int _selectedSegmentIndex;
		private int _draggingHandleIndex;
		private int _mouseOverHandleIndex;
		private int _handleIndexToDisplayAsTransform;

		private bool _shiftLastFrame;
		private bool _hasUpdatedScreenSpaceLine;
		private bool _hasUpdatedNormalsVertexPath;
		private bool _editingNormalsOld;

		private Vector3 _transformPos;
		private Vector3 _transformScale;
		private Quaternion _transformRot;

		private Color _handlesStartCol;

		// Constants
		private const int BezierPathTab = 0;
		private const int VertexPathTab = 1;

		#endregion

		#region Inspectors

		public override void OnInspectorGUI()
		{
			// Initialize GUI styles
			if (_boldFoldoutStyle == null)
			{
				_boldFoldoutStyle = new GUIStyle(EditorStyles.foldout)
				{
					fontStyle = FontStyle.Bold
				};
			}

			Undo.RecordObject(_creator, "Path settings changed");

			// Draw Bezier and Vertex tabs
			var tabIndex = GUILayout.Toolbar(Data.tabIndex, TabNames);
			if (tabIndex != Data.tabIndex)
			{
				Data.tabIndex = tabIndex;
				TabChanged();
			}

			// Draw inspector for active tab
			switch (Data.tabIndex)
			{
				case BezierPathTab:
					DrawBezierPathInspector();
					break;
				case VertexPathTab:
					DrawVertexPathInspector();
					break;
			}

			// Notify of undo/redo that might modify the path
			if (Event.current.type == EventType.ValidateCommand && Event.current.commandName == "UndoRedoPerformed")
			{
				Data.PathModifiedByUndo();
			}
		}

		private void DrawBezierPathInspector()
		{
			using (var check = new EditorGUI.ChangeCheckScope())
			{
				// Path options:
				Data.arePathOptionsShown = EditorGUILayout.Foldout(Data.arePathOptionsShown, new GUIContent("Bézier Path Options"), true, _boldFoldoutStyle);
				if (Data.arePathOptionsShown)
				{
					BezierPath.ControlPointMode = (BezierPath.ControlMode)EditorGUILayout.EnumPopup(new GUIContent("Control Mode"), BezierPath.ControlPointMode);
					if (BezierPath.ControlPointMode == BezierPath.ControlMode.Automatic)
					{
						BezierPath.AutoControlLength = EditorGUILayout.Slider(new GUIContent("Control Spacing"), BezierPath.AutoControlLength, 0, 1);
					}
					
					Data.isTransformToolShown = EditorGUILayout.Toggle(new GUIContent("Enable Transforms"), Data.isTransformToolShown);

					Tools.hidden = !Data.isTransformToolShown;

					// Check if out of bounds (can occur after undo operations)
					if (_handleIndexToDisplayAsTransform >= BezierPath.NumPoints)
					{
						_handleIndexToDisplayAsTransform = -1;
					}

					// If a point has been selected
					if (_handleIndexToDisplayAsTransform != -1)
					{
						EditorGUILayout.LabelField("Selected Point:");

						using (new EditorGUI.IndentLevelScope())
						{
							var currentPosition = _creator.BezierPath[_handleIndexToDisplayAsTransform];
							var newPosition = EditorGUILayout.Vector3Field("Position", currentPosition);
							if (newPosition != currentPosition)
							{
								Undo.RecordObject(_creator, "Move point");
								_creator.BezierPath.MovePoint(_handleIndexToDisplayAsTransform, newPosition);
							}
							// Don't draw the angle field if we aren't selecting an anchor point/not in 3d space
							if (_handleIndexToDisplayAsTransform % 3 == 0)
							{
								var anchorIndex = _handleIndexToDisplayAsTransform / 3;
								var currentAngle = _creator.BezierPath.GetAnchorNormalAngle(anchorIndex);
								var newAngle = EditorGUILayout.FloatField("Angle", currentAngle);
								if (!Mathf.Approximately(currentAngle, newAngle))
								{
									Undo.RecordObject(_creator, "Set Angle");
									_creator.BezierPath.SetAnchorNormalAngle(anchorIndex, newAngle);
								}
							}
						}
					}

					if (Data.isTransformToolShown & (_handleIndexToDisplayAsTransform == -1))
					{
						if (GUILayout.Button("Centre Transform"))
						{
							var worldCentre = BezierPath.CalculateBoundsWithTransform(_creator.transform).center;
							var transformPos = _creator.transform.position;
							var worldCentreToTransform = transformPos - worldCentre;

							if (worldCentre != _creator.transform.position)
							{
								//Undo.RecordObject (creator, "Centralize Transform");
								if (worldCentreToTransform != Vector3.zero)
								{
									var localCentreToTransform = MathUtility.InverseTransformVector(worldCentreToTransform, _creator.transform);
									for (var i = 0; i < BezierPath.NumPoints; i++)
									{
										BezierPath.SetPoint(i, BezierPath.GetPoint(i) + localCentreToTransform, true);
									}
								}

								_creator.transform.position = worldCentre;
								BezierPath.NotifyPathModified();
							}
						}
					}

					if (GUILayout.Button("Reset Path"))
					{
						Undo.RecordObject(_creator, "Reset Path");
						var in2DEditorMode = EditorSettings.defaultBehaviorMode == EditorBehaviorMode.Mode2D;
						Data.ResetBezierPath(_creator.transform.position, in2DEditorMode);
						EditorApplication.QueuePlayerLoopUpdate();
					}

					GUILayout.Space(InspectorSectionSpacing);
				}

				Data.areNormalsShown = EditorGUILayout.Foldout(Data.areNormalsShown, new GUIContent("Normals Options"), true, _boldFoldoutStyle);
				if (Data.areNormalsShown)
				{
					BezierPath.AreNormalsFlipped = EditorGUILayout.Toggle(new GUIContent("Flip Normals"), BezierPath.AreNormalsFlipped);
					BezierPath.GlobalNormalsAngle = EditorGUILayout.Slider(new GUIContent("Global Angle"), BezierPath.GlobalNormalsAngle, 0, 360);

					if (GUILayout.Button("Reset Normals"))
					{
						Undo.RecordObject(_creator, "Reset Normals");
						BezierPath.AreNormalsFlipped = false;
						BezierPath.ResetNormalAngles();
					}
					GUILayout.Space(InspectorSectionSpacing);
				}

				// Editor display options
				Data.areDisplayOptionsShown = EditorGUILayout.Foldout(Data.areDisplayOptionsShown, new GUIContent("Display Options"), true, _boldFoldoutStyle);
				if (Data.areDisplayOptionsShown)
				{
					Data.arePathBoundsShown = GUILayout.Toggle(Data.arePathBoundsShown, new GUIContent("Show Path Bounds"));
					Data.arePerSegmentBoundsShown = GUILayout.Toggle(Data.arePerSegmentBoundsShown, new GUIContent("Show Segment Bounds"));
					Data.areAnchorPointsDisplayed = GUILayout.Toggle(Data.areAnchorPointsDisplayed, new GUIContent("Show Anchor Points"));
					if (!(BezierPath.ControlPointMode == BezierPath.ControlMode.Automatic && _globalDisplaySettings.isAutoControlsHided))
					{
						Data.areControlPointsDisplayed = GUILayout.Toggle(Data.areControlPointsDisplayed, new GUIContent("Show Control Points"));
					}
					Data.isConstantHandleSizeKept = GUILayout.Toggle(Data.isConstantHandleSizeKept, new GUIContent("Constant Point Size", ConstantSizeTooltip));
					Data.bezierHandleScale = Mathf.Max(0, EditorGUILayout.FloatField(new GUIContent("Handle Scale"), Data.bezierHandleScale));
					DrawGlobalDisplaySettingsInspector();
				}

				if (check.changed)
				{
					SceneView.RepaintAll();
					EditorApplication.QueuePlayerLoopUpdate();
				}
			}
		}

		private void DrawVertexPathInspector()
		{

			GUILayout.Space(InspectorSectionSpacing);
			EditorGUILayout.LabelField("Vertex count: " + _creator.Path.NumPoints);
			GUILayout.Space(InspectorSectionSpacing);

			Data.areVertexPathOptionsShown = EditorGUILayout.Foldout(Data.areVertexPathOptionsShown, new GUIContent("Vertex Path Options"), true, _boldFoldoutStyle);
			if (Data.areVertexPathOptionsShown)
			{
				using (var check = new EditorGUI.ChangeCheckScope())
				{
					Data.vertexPathMaxAngleError = EditorGUILayout.Slider(new GUIContent("Max Angle Error"), Data.vertexPathMaxAngleError, 0, 45);
					Data.vertexPathMinVertexSpacing = EditorGUILayout.Slider(new GUIContent("Min Vertex Dst"), Data.vertexPathMinVertexSpacing, 0, 1);

					GUILayout.Space(InspectorSectionSpacing);
					if (check.changed)
					{
						Data.VertexPathSettingsChanged();
						SceneView.RepaintAll();
						EditorApplication.QueuePlayerLoopUpdate();
					}
				}
			}

			Data.areVertexPathDisplayOptionsShown = EditorGUILayout.Foldout(Data.areVertexPathDisplayOptionsShown, new GUIContent("Display Options"), true, _boldFoldoutStyle);
			if (Data.areVertexPathDisplayOptionsShown)
			{
				using (var check = new EditorGUI.ChangeCheckScope())
				{
					Data.areNormalsShownInVertexMode = GUILayout.Toggle(Data.areNormalsShownInVertexMode, new GUIContent("Show Normals"));
					Data.isBezierPathShownInVertexMode = GUILayout.Toggle(Data.isBezierPathShownInVertexMode, new GUIContent("Show Bezier Path"));

					if (check.changed)
					{
						SceneView.RepaintAll();
						EditorApplication.QueuePlayerLoopUpdate();
					}
				}
				DrawGlobalDisplaySettingsInspector();
			}
		}

		private void DrawGlobalDisplaySettingsInspector()
		{
			using (var check = new EditorGUI.ChangeCheckScope())
			{
				Data.areGlobalDisplaySettingsFoldedOut = EditorGUILayout.InspectorTitlebar(Data.areGlobalDisplaySettingsFoldedOut, _globalDisplaySettings);
				if (Data.areGlobalDisplaySettingsFoldedOut)
				{
					CreateCachedEditor(_globalDisplaySettings, null, ref _globalDisplaySettingsEditor);
					_globalDisplaySettingsEditor.OnInspectorGUI();
				}
				if (check.changed)
				{
					UpdateGlobalDisplaySettings();
					SceneView.RepaintAll();
				}
			}
		}

		#endregion

		#region Scene GUI

		private void OnSceneGUI()
		{
			if (!_globalDisplaySettings.isVisibleBehindObjects)
			{
				Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
			}

			var eventType = Event.current.type;

			using (var check = new EditorGUI.ChangeCheckScope())
			{
				_handlesStartCol = Handles.color;
				switch (Data.tabIndex)
				{
					case BezierPathTab:
						if (eventType != EventType.Repaint && eventType != EventType.Layout)
						{
							ProcessBezierPathInput(Event.current);
						}
						DrawBezierPathSceneEditor();
						break;
					case VertexPathTab:
						if (eventType == EventType.Repaint)
						{
							DrawVertexPathSceneEditor();
						}
						break;
				}

				// Don't allow clicking over empty space to deselect the object
				if (eventType == EventType.Layout)
				{
					HandleUtility.AddDefaultControl(0);
				}

				if (check.changed)
				{
					EditorApplication.QueuePlayerLoopUpdate();
				}
			}

			SetTransformState();
		}

		private void DrawVertexPathSceneEditor()
		{
			var bezierCol = _globalDisplaySettings.bezierPath;
			bezierCol.a *= .5f;

			if (Data.isBezierPathShownInVertexMode)
			{
				for (var i = 0; i < BezierPath.NumSegments; i++)
				{
					var points = BezierPath.GetPointsInSegment(i);
					for (var j = 0; j < points.Length; j++)
					{
						points[j] = MathUtility.TransformPoint(points[j], _creator.transform);
					}
					Handles.DrawBezier(points[0], points[3], points[1], points[2], bezierCol, null, 2);
				}
			}

			Handles.color = _globalDisplaySettings.vertexPath;

			for (var i = 0; i < _creator.Path.NumPoints; i++)
			{
				var nextIndex = (i + 1) % _creator.Path.NumPoints;
				if (nextIndex != 0)
				{
					Handles.DrawLine(_creator.Path.GetPoint(i), _creator.Path.GetPoint(nextIndex));
				}
			}

			if (Data.areNormalsShownInVertexMode)
			{
				Handles.color = _globalDisplaySettings.normals;
				var normalLines = new Vector3[_creator.Path.NumPoints * 2];
				for (var i = 0; i < _creator.Path.NumPoints; i++)
				{
					normalLines[i * 2] = _creator.Path.GetPoint(i);
					normalLines[i * 2 + 1] = _creator.Path.GetPoint(i) + _creator.Path.localNormals[i] * _globalDisplaySettings.normalsLength;
				}
				Handles.DrawLines(normalLines);
			}
		}

		private void ProcessBezierPathInput(Event e)
		{
			// Find which handle mouse is over. Start by looking at previous handle index first, as most likely to still be closest to mouse
			var previousMouseOverHandleIndex = (_mouseOverHandleIndex == -1) ? 0 : _mouseOverHandleIndex;
			_mouseOverHandleIndex = -1;
			for (var i = 0; i < BezierPath.NumPoints; i += 3)
			{

				var handleIndex = (previousMouseOverHandleIndex + i) % BezierPath.NumPoints;
				var handleRadius = GetHandleDiameter(_globalDisplaySettings.anchorSize * Data.bezierHandleScale, BezierPath[handleIndex]) / 2f;
				var pos = MathUtility.TransformPoint(BezierPath[handleIndex], _creator.transform);
				var dst = HandleUtility.DistanceToCircle(pos, handleRadius);
				if (dst == 0)
				{
					_mouseOverHandleIndex = handleIndex;
					break;
				}
			}

			// Shift-left click (when mouse not over a handle) to split or add segment
			if (_mouseOverHandleIndex == -1)
			{
				if (e.type == EventType.MouseDown && e.button == 0 && e.shift)
				{
					UpdatePathMouseInfo();
					// Insert point along selected segment
					if (_selectedSegmentIndex != -1 && _selectedSegmentIndex < BezierPath.NumSegments)
					{
						var newPathPoint = _pathMouseInfo.closestWorldPointToMouse;
						newPathPoint = MathUtility.InverseTransformPoint(newPathPoint, _creator.transform);
						Undo.RecordObject(_creator, "Split segment");
						BezierPath.SplitSegment(newPathPoint, _selectedSegmentIndex, _pathMouseInfo.timeOnBezierSegment);
					}
					// If path is not a closed loop, add new point on to the end of the path
					else
					{
						// If control/command are held down, the point gets pre-pended, so we want to check distance
						// to the endpoint we are adding to
						var pointIdx = e.control || e.command ? 0 : BezierPath.NumPoints - 1;
						// insert new point at same dst from scene camera as the point that comes before it (for a 3d path)
						var endPointLocal = BezierPath[pointIdx];
						var endPointGlobal =
							MathUtility.TransformPoint(endPointLocal, _creator.transform);
						var distanceCameraToEndpoint = (Camera.current.transform.position - endPointGlobal).magnitude;
						var newPointGlobal =
							MouseUtility.GetMouseWorldPosition(distanceCameraToEndpoint);
						var newPointLocal =
							MathUtility.InverseTransformPoint(newPointGlobal, _creator.transform);

						Undo.RecordObject(_creator, "Add segment");
						if (e.control || e.command)
						{
							BezierPath.AddSegmentToStart(newPointLocal);
						}
						else
						{
							BezierPath.AddSegmentToEnd(newPointLocal);
						}
					}
				}
			}

			// Control click or backspace/delete to remove point
			if (e.keyCode == KeyCode.Backspace || e.keyCode == KeyCode.Delete || ((e.control || e.command) && e.type == EventType.MouseDown && e.button == 0))
			{

				if (_mouseOverHandleIndex != -1)
				{
					Undo.RecordObject(_creator, "Delete segment");
					BezierPath.DeleteSegment(_mouseOverHandleIndex);
					if (_mouseOverHandleIndex == _handleIndexToDisplayAsTransform)
					{
						_handleIndexToDisplayAsTransform = -1;
					}
					_mouseOverHandleIndex = -1;
					Repaint();
				}
			}

			// Holding shift and moving mouse (but mouse not over a handle/dragging a handle)
			if (_draggingHandleIndex == -1 && _mouseOverHandleIndex == -1)
			{
				var shiftDown = e.shift && !_shiftLastFrame;
				if (shiftDown || ((e.type == EventType.MouseMove || e.type == EventType.MouseDrag) && e.shift))
				{
					UpdatePathMouseInfo();
					var notSplittingAtControlPoint = _pathMouseInfo.timeOnBezierSegment > 0 && _pathMouseInfo.timeOnBezierSegment < 1;
					if (_pathMouseInfo.mouseDstToLine < SegmentSelectDistanceThreshold && notSplittingAtControlPoint)
					{
						if (_pathMouseInfo.closestSegmentIndex != _selectedSegmentIndex)
						{
							_selectedSegmentIndex = _pathMouseInfo.closestSegmentIndex;
							HandleUtility.Repaint();
						}
					}
					else
					{
						_selectedSegmentIndex = -1;
						HandleUtility.Repaint();
					}

				}
			}

			_shiftLastFrame = e.shift;
		}

		private void DrawBezierPathSceneEditor()
		{
			var displayControlPoints = Data.areControlPointsDisplayed && (BezierPath.ControlPointMode != BezierPath.ControlMode.Automatic || !_globalDisplaySettings.isAutoControlsHided);
			var bounds = BezierPath.CalculateBoundsWithTransform(_creator.transform);

			if (Event.current.type == EventType.Repaint)
			{
				for (var i = 0; i < BezierPath.NumSegments; i++)
				{
					var points = BezierPath.GetPointsInSegment(i);
					for (var j = 0; j < points.Length; j++)
					{
						points[j] = MathUtility.TransformPoint(points[j], _creator.transform);
					}

					if (Data.arePerSegmentBoundsShown)
					{
						var segmentBounds = CubicBezierUtility.CalculateSegmentBounds(points[0], points[1], points[2], points[3]);
						Handles.color = _globalDisplaySettings.segmentBounds;
						Handles.DrawWireCube(segmentBounds.center, segmentBounds.size);
					}

					// Draw lines between control points
					if (displayControlPoints)
					{
						Handles.color = (BezierPath.ControlPointMode == BezierPath.ControlMode.Automatic) ? _globalDisplaySettings.handleDisabled : _globalDisplaySettings.controlLine;
						Handles.DrawLine(points[1], points[0]);
						Handles.DrawLine(points[2], points[3]);
					}

					// Draw path
					var highlightSegment = (i == _selectedSegmentIndex && Event.current.shift && _draggingHandleIndex == -1 && _mouseOverHandleIndex == -1);
					var segmentCol = (highlightSegment) ? _globalDisplaySettings.highlightedPath : _globalDisplaySettings.bezierPath;
					Handles.DrawBezier(points[0], points[3], points[1], points[2], segmentCol, null, 2);
				}

				if (Data.arePathBoundsShown)
				{
					Handles.color = _globalDisplaySettings.bounds;
					Handles.DrawWireCube(bounds.center, bounds.size);
				}

				// Draw normals
				if (Data.areNormalsShown)
				{
					if (!_hasUpdatedNormalsVertexPath)
					{
						_normalsVertexPath = new VertexPath(BezierPath, _creator.transform, NormalsSpacing);
						_hasUpdatedNormalsVertexPath = true;
					}

					if (_editingNormalsOld != Data.areNormalsShown)
					{
						_editingNormalsOld = Data.areNormalsShown;
						Repaint();
					}

					var normalLines = new Vector3[_normalsVertexPath.NumPoints * 2];
					Handles.color = _globalDisplaySettings.normals;
					for (var i = 0; i < _normalsVertexPath.NumPoints; i++)
					{
						normalLines[i * 2] = _normalsVertexPath.GetPoint(i);
						normalLines[i * 2 + 1] = _normalsVertexPath.GetPoint(i) + _normalsVertexPath.GetNormal(i) * _globalDisplaySettings.normalsLength;
					}
					Handles.DrawLines(normalLines);
				}
			}

			if (Data.areAnchorPointsDisplayed)
			{
				for (var i = 0; i < BezierPath.NumPoints; i += 3)
				{
					DrawHandle(i);
				}
			}
			if (displayControlPoints)
			{
				for (var i = 1; i < BezierPath.NumPoints - 1; i += 3)
				{
					DrawHandle(i);
					DrawHandle(i + 1);
				}
			}
		}

		private void DrawHandle(int i)
		{
			var handlePosition = MathUtility.TransformPoint(BezierPath[i], _creator.transform);

			var anchorHandleSize = GetHandleDiameter(_globalDisplaySettings.anchorSize * Data.bezierHandleScale, BezierPath[i]);
			var controlHandleSize = GetHandleDiameter(_globalDisplaySettings.controlSize * Data.bezierHandleScale, BezierPath[i]);

			var isAnchorPoint = i % 3 == 0;
			var isInteractive = isAnchorPoint || BezierPath.ControlPointMode != BezierPath.ControlMode.Automatic;
			var handleSize = (isAnchorPoint) ? anchorHandleSize : controlHandleSize;
			var doTransformHandle = i == _handleIndexToDisplayAsTransform;

			var handleColours = (isAnchorPoint) ? _splineAnchorColours : _splineControlColours;
			if (i == _handleIndexToDisplayAsTransform)
			{
				handleColours.defaultColour = (isAnchorPoint) ? _globalDisplaySettings.anchorSelected : _globalDisplaySettings.controlSelected;
			}
			var cap = _capFunctions[(isAnchorPoint) ? _globalDisplaySettings.anchorShape : _globalDisplaySettings.controlShape];
			PathHandle.HandleInputType handleInputType;
			handlePosition = PathHandle.DrawHandle(handlePosition, isInteractive, handleSize, cap, handleColours, out handleInputType, i);

			if (doTransformHandle)
			{
				// Show normals rotate tool 
				if (Data.areNormalsShown && Tools.current == Tool.Rotate && isAnchorPoint)
				{
					Handles.color = _handlesStartCol;

					var attachedControlIndex = (i == BezierPath.NumPoints - 1) ? i - 1 : i + 1;
					var dir = (BezierPath[attachedControlIndex] - handlePosition).normalized;
					var handleRotOffset = (360 + BezierPath.GlobalNormalsAngle) % 360;
					_anchorAngleHandle.radius = handleSize * 3;
					_anchorAngleHandle.angle = handleRotOffset + BezierPath.GetAnchorNormalAngle(i / 3);
					var handleDirection = Vector3.Cross(dir, Vector3.up);
					var handleMatrix = Matrix4x4.TRS(
						handlePosition,
						Quaternion.LookRotation(handleDirection, dir),
						Vector3.one
					);

					using (new Handles.DrawingScope(handleMatrix))
					{
						// draw the handle
						EditorGUI.BeginChangeCheck();
						_anchorAngleHandle.DrawHandle();
						if (EditorGUI.EndChangeCheck())
						{
							Undo.RecordObject(_creator, "Set angle");
							BezierPath.SetAnchorNormalAngle(i / 3, _anchorAngleHandle.angle - handleRotOffset);
						}
					}
				}
				else
				{
					handlePosition = Handles.DoPositionHandle(handlePosition, Quaternion.identity);
				}

			}

			switch (handleInputType)
			{
				case PathHandle.HandleInputType.LmbDrag:
					_draggingHandleIndex = i;
					_handleIndexToDisplayAsTransform = -1;
					Repaint();
					break;
				case PathHandle.HandleInputType.LmbRelease:
					_draggingHandleIndex = -1;
					_handleIndexToDisplayAsTransform = -1;
					Repaint();
					break;
				case PathHandle.HandleInputType.LmbClick:
					_draggingHandleIndex = -1;
					if (Event.current.shift)
					{
						_handleIndexToDisplayAsTransform = -1; // disable move tool if new point added
					}
					else
					{
						if (_handleIndexToDisplayAsTransform == i)
						{
							_handleIndexToDisplayAsTransform = -1; // disable move tool if clicking on point under move tool
						}
						else
						{
							_handleIndexToDisplayAsTransform = i;
						}
					}
					Repaint();
					break;
				case PathHandle.HandleInputType.LmbPress:
					if (_handleIndexToDisplayAsTransform != i)
					{
						_handleIndexToDisplayAsTransform = -1;
						Repaint();
					}
					break;
				case PathHandle.HandleInputType.None:
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			var localHandlePosition = MathUtility.InverseTransformPoint(handlePosition, _creator.transform);

			if (BezierPath[i] != localHandlePosition)
			{
				Undo.RecordObject(_creator, "Move point");
				BezierPath.MovePoint(i, localHandlePosition);
			}
		}

		#endregion

		#region Internal methods

		private void OnDisable()
		{
			Tools.hidden = false;
		}

		private void OnEnable()
		{
			_creator = (Runtime.Objects.PathCreator)target;
			var in2DEditorMode = EditorSettings.defaultBehaviorMode == EditorBehaviorMode.Mode2D;
			_creator.InitializeEditorData(in2DEditorMode);

			Data.OnBezierCreated -= ResetState;
			Data.OnBezierCreated += ResetState;
			Undo.undoRedoPerformed -= OnUndoRedo;
			Undo.undoRedoPerformed += OnUndoRedo;

			LoadDisplaySettings();
			UpdateGlobalDisplaySettings();
			ResetState();
			SetTransformState(true);
		}

		private void SetTransformState(bool initialize = false)
		{
			var t = _creator.transform;
			if (!initialize)
			{
				if (_transformPos != t.position || t.localScale != _transformScale || t.rotation != _transformRot)
				{
					Data.PathTransformed();
				}
			}
			_transformPos = t.position;
			_transformScale = t.localScale;
			_transformRot = t.rotation;
		}

		private void OnUndoRedo()
		{
			_hasUpdatedScreenSpaceLine = false;
			_hasUpdatedNormalsVertexPath = false;
			_selectedSegmentIndex = -1;

			Repaint();
		}

		private static void TabChanged()
		{
			SceneView.RepaintAll();
			RepaintUnfocusedSceneViews();
		}

		private void LoadDisplaySettings()
		{
			_globalDisplaySettings = GlobalDisplaySettings.Load();

			_capFunctions = new Dictionary<GlobalDisplaySettings.HandleType, Handles.CapFunction>
			{
				{ GlobalDisplaySettings.HandleType.Circle, Handles.CylinderHandleCap },
				{ GlobalDisplaySettings.HandleType.Sphere, Handles.SphereHandleCap },
				{ GlobalDisplaySettings.HandleType.Square, Handles.CubeHandleCap }
			};
		}

		private void UpdateGlobalDisplaySettings()
		{
			var gds = _globalDisplaySettings;
			_splineAnchorColours = new PathHandle.HandleColours(gds.anchor, gds.anchorHighlighted, gds.anchorSelected, gds.handleDisabled);
			_splineControlColours = new PathHandle.HandleColours(gds.control, gds.controlHighlighted, gds.controlSelected, gds.handleDisabled);

			_anchorAngleHandle.fillColor = new Color(1.0f, 1.0f, 1.0f, 0.05f);
			_anchorAngleHandle.wireframeColor = Color.grey;
			_anchorAngleHandle.radiusHandleColor = Color.clear;
			_anchorAngleHandle.angleHandleColor = Color.white;
		}

		private void ResetState()
		{
			_selectedSegmentIndex = -1;
			_draggingHandleIndex = -1;
			_mouseOverHandleIndex = -1;
			_handleIndexToDisplayAsTransform = -1;
			_hasUpdatedScreenSpaceLine = false;
			_hasUpdatedNormalsVertexPath = false;

			BezierPath.OnModified -= OnPathModified;
			BezierPath.OnModified += OnPathModified;

			SceneView.RepaintAll();
			EditorApplication.QueuePlayerLoopUpdate();
		}

		private void OnPathModified()
		{
			_hasUpdatedScreenSpaceLine = false;
			_hasUpdatedNormalsVertexPath = false;

			RepaintUnfocusedSceneViews();
		}

		private static void RepaintUnfocusedSceneViews()
		{
			// If multiple scene views are open, repaint those which do not have focus.
			if (SceneView.sceneViews.Count > 1)
			{
				foreach (SceneView sv in SceneView.sceneViews)
				{
					if (EditorWindow.focusedWindow != (EditorWindow)sv)
					{
						sv.Repaint();
					}
				}
			}
		}

		private void UpdatePathMouseInfo()
		{

			if (!_hasUpdatedScreenSpaceLine || (_screenSpaceLine != null && _screenSpaceLine.TransformIsOutOfDate()))
			{
				_screenSpaceLine = new ScreenSpacePolyLine(BezierPath, _creator.transform, ScreenPolylineMaxAngleError, ScreenPolylineMinVertexDst);
				_hasUpdatedScreenSpaceLine = true;
			}

			Debug.Assert(_screenSpaceLine != null, nameof(_screenSpaceLine) + " != null");
			
			_pathMouseInfo = _screenSpaceLine.CalculateMouseInfo();
		}

		private float GetHandleDiameter(float diameter, Vector3 handlePosition)
		{
			var scaledDiameter = diameter * ConstantHandleScale;
			if (Data.isConstantHandleSizeKept)
			{
				scaledDiameter *= HandleUtility.GetHandleSize(handlePosition) * 2.5f;
			}
			return scaledDiameter;
		}

		private BezierPath BezierPath => Data.BezierPath;

		private PathCreatorData Data => _creator.EditorData;

		private bool EditingNormals => Tools.current == Tool.Rotate && _handleIndexToDisplayAsTransform % 3 == 0;

		#endregion
	}
}