using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Resources.PathCreator.Core.Editor.Helper
{
	public static class PathHandle
	{
		private const float ExtraInputRadius = .005f;

		private static Vector2 _handleDragMouseStart;
		private static Vector2 _handleDragMouseEnd;
		private static Vector3 _handleDragWorldStart;

		private static int _selectedHandleID;
		private static bool _mouseIsOverAHandle;

		public enum HandleInputType
		{
			None,
			LmbPress,
			LmbClick,
			LmbDrag,
			LmbRelease,
		};

		private static float _dstMouseToDragPointStart;

		private static readonly List<int> Ids;
		private static readonly HashSet<int> IDHash;

		static PathHandle()
		{
			Ids = new List<int>();
			IDHash = new HashSet<int>();

			_dstMouseToDragPointStart = float.MaxValue;
		}

		public static Vector3 DrawHandle(Vector3 position, bool isInteractive, float handleDiameter, Handles.CapFunction capFunc, HandleColours colours, out HandleInputType inputType, int handleIndex)
		{
			var id = GetID(handleIndex);
			var screenPosition = Handles.matrix.MultiplyPoint(position);
			var cachedMatrix = Handles.matrix;

			inputType = HandleInputType.None;

			var eventType = Event.current.GetTypeForControl(id);
			var handleRadius = handleDiameter / 2f;
			var dstToHandle = HandleUtility.DistanceToCircle(position, handleRadius + ExtraInputRadius);
			var dstToMouse = HandleUtility.DistanceToCircle(position, 0);

			// Handle input events
			if (isInteractive)
			{
				// Repaint if mouse is entering/exiting handle (for highlight colour)
				if (dstToHandle == 0)
				{
					if (!_mouseIsOverAHandle)
					{
						HandleUtility.Repaint();
						_mouseIsOverAHandle = true;
					}
				}
				else
				{
					if (_mouseIsOverAHandle)
					{
						HandleUtility.Repaint();
						_mouseIsOverAHandle = false;
					}
				}
				switch (eventType)
				{
					case EventType.MouseDown:
						if (Event.current.button == 0 && Event.current.modifiers != EventModifiers.Alt)
						{
							if (dstToHandle == 0 && dstToMouse < _dstMouseToDragPointStart)
							{
								_dstMouseToDragPointStart = dstToMouse;
								GUIUtility.hotControl = id;
								_handleDragMouseEnd = _handleDragMouseStart = Event.current.mousePosition;
								_handleDragWorldStart = position;
								_selectedHandleID = id;
								inputType = HandleInputType.LmbPress;
							}
						}
						break;

					case EventType.MouseUp:
						_dstMouseToDragPointStart = float.MaxValue;
						if (GUIUtility.hotControl == id && Event.current.button == 0)
						{
							GUIUtility.hotControl = 0;
							_selectedHandleID = -1;
							Event.current.Use();

							inputType = HandleInputType.LmbRelease;


							if (Event.current.mousePosition == _handleDragMouseStart)
							{
								inputType = HandleInputType.LmbClick;
							}
						}
						break;

					case EventType.MouseDrag:
						if (GUIUtility.hotControl == id && Event.current.button == 0)
						{
							_handleDragMouseEnd += new Vector2(Event.current.delta.x, -Event.current.delta.y);
							var position2 = Camera.current.WorldToScreenPoint(Handles.matrix.MultiplyPoint(_handleDragWorldStart))
							                + (Vector3)(_handleDragMouseEnd - _handleDragMouseStart);
							inputType = HandleInputType.LmbDrag;
							// Handle can move freely in 3d space
							position = Handles.matrix.inverse.MultiplyPoint(Camera.current.ScreenToWorldPoint(position2));

							GUI.changed = true;
							Event.current.Use();
						} 
						break;
				}
			}

			switch (eventType)
			{
				case EventType.Repaint:
					var originalColour = Handles.color;
					Handles.color = (isInteractive) ? colours.defaultColour : colours.disabledColour;

					if (id == GUIUtility.hotControl)
					{
						Handles.color = colours.selectedColour;
					}
					else if (dstToHandle == 0 && _selectedHandleID == -1 && isInteractive)
					{
						Handles.color = colours.highlightedColour;
					}

					Handles.matrix = Matrix4x4.identity;
					var lookForward = Vector3.zero;
					var cam = Camera.current;
					if (cam != null)
					{
						if (cam.orthographic)
						{
							lookForward = -cam.transform.forward;
						}
						else
						{
							lookForward = (cam.transform.position - position).normalized;
						}
					}
					
					if (lookForward == Vector3.zero) 
					{
						lookForward = Vector3.forward;
					}

					capFunc(id, screenPosition, Quaternion.LookRotation(lookForward), handleDiameter, EventType.Repaint);
					Handles.matrix = cachedMatrix;

					Handles.color = originalColour;
					break;

				case EventType.Layout:
					Handles.matrix = Matrix4x4.identity;
					HandleUtility.AddControl(id, HandleUtility.DistanceToCircle(screenPosition, handleDiameter / 2f));
					Handles.matrix = cachedMatrix;
					break;
			}

			return position;
		}

		public struct HandleColours
		{
			public Color defaultColour;
			public Color highlightedColour;
			public Color selectedColour;
			public Color disabledColour;

			public HandleColours(Color defaultColour, Color highlightedColour, Color selectedColour, Color disabledColour)
			{
				this.defaultColour = defaultColour;
				this.highlightedColour = highlightedColour;
				this.selectedColour = selectedColour;
				this.disabledColour = disabledColour;
			}
		}

		private static void AddIDs(int upToIndex)
		{
			var numIDAtStart = Ids.Count;
			var numToAdd = (upToIndex - numIDAtStart) + 1;
			for (var i = 0; i < numToAdd; i++)
			{
				var hashString = string.Format("pathhandle({0})", numIDAtStart + i);
				var hash = hashString.GetHashCode();

				var id = GUIUtility.GetControlID(hash, FocusType.Passive);
				var numIts = 0;

				// This is a bit of a shot in the dark at fixing a reported bug that I've been unable to reproduce.
				// The problem is that multiple handles are being selected when just one is clicked on.
				// I assume this is because they're somehow being assigned the same id.
				while (IDHash.Contains(id))
				{
					numIts++;
					id += numIts * numIts;
					if (numIts > 100)
					{
						Debug.LogError("Failed to generate unique handle id.");
						break;
					}
				}

				IDHash.Add(id);
				Ids.Add(id);
			}
		}

		private static int GetID(int handleIndex)
		{
			if (handleIndex >= Ids.Count)
			{
				AddIDs(handleIndex);
			}

			return Ids[handleIndex];
		}
	}
}