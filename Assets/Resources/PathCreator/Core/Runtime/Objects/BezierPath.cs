using System;
using System.Collections.Generic;
using Resources.PathCreator.Core.Runtime.Utility;
using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Objects
{
	/// A bezier path is a path made by stitching together any number of (cubic) bezier curves.
	/// A single cubic bezier curve is defined by 4 points: anchor1, control1, control2, anchor2
	/// The curve moves between the 2 anchors, and the shape of the curve is affected by the positions of the 2 control points

	/// Apart from storing the points, this class also provides methods for working with the path.
	/// For example, adding, inserting, and deleting points.

	[Serializable]
	public class BezierPath
	{
		#region Events

		public event Action OnModified;

		#endregion


		#region Enums

		public enum ControlMode
		{
			Aligned, 
			Mirrored, 
			Free, 
			Automatic
		};

		#endregion
		

		#region Fields

		[SerializeField, HideInInspector] private List<Vector3> points;
		[SerializeField, HideInInspector] private ControlMode controlMode = ControlMode.Mirrored;
		[SerializeField, HideInInspector] private float autoControlLength = 0.3f;
		[SerializeField, HideInInspector] private bool areBoundsUpToDated;

		// Normals settings
		[SerializeField, HideInInspector] private List<float> perAnchorNormalsAngle;
		[SerializeField, HideInInspector] private float globalNormalsAngle;
		[SerializeField, HideInInspector] private bool areNormalsFlipped;

        #endregion
        
        
        #region Constructors
        
        public BezierPath() : this(Vector3.zero) { }

        /// <summary> Creates a two-anchor path centred around the given centre point </summary>
        /// <param name = "center"> Center point </param>
        public BezierPath(Vector3 center)
		{
			const int height = 2;
			const float controlHeight = 0.5f;
			
			points = new List<Vector3> 
			{
				center,
				center + Vector3.up * controlHeight + Vector3.right * controlHeight,
				center + Vector3.up * (height - controlHeight) + Vector3.left * controlHeight,
				center + Vector3.up * height
			};

			perAnchorNormalsAngle = new List<float>() { 0, 0 };
		}

		#endregion

		
		#region Public methods and accessors

		/// Get world space position of point
		public Vector3 this[int i] => GetPoint(i);

		/// Get world space position of point
		public Vector3 GetPoint(int i)
		{
			return points[i];
		}

		/// Get world space position of point
		public void SetPoint(int i, Vector3 localPosition, bool suppressPathModifiedEvent = false)
		{
			points[i] = localPosition;

			if (!suppressPathModifiedEvent)
			{
				NotifyPathModified();
			}
		}

		/// Total number of points in the path (anchors and controls)
		public int NumPoints => points.Count;

		/// Number of anchor points making up the path
		public int NumAnchorPoints => (points.Count + 2) / 3;

		/// Number of bezier curves making up this path
		public int NumSegments => points.Count / 3;

		/// The control mode determines the behaviour of control points.
		/// Possible modes are:
		/// Aligned = controls stay in straight line around their anchor
		/// Mirrored = controls stay in straight, equidistant line around their anchor
		/// Free = no constraints (use this if sharp corners are needed)
		/// Automatic = controls placed automatically to try make the path smooth
		public ControlMode ControlPointMode
		{
			get => controlMode;
			
			set
			{
				if (controlMode != value)
				{
					controlMode = value;
					
					if (controlMode == ControlMode.Automatic)
					{
						AutoSetAllControlPoints();
						
						NotifyPathModified();
					}
				}
			}
		}

		/// When using automatic control point placement, this value scales how far apart controls are placed
		public float AutoControlLength
		{
			get => autoControlLength;
			
			set
			{
				value = Mathf.Max(value, 0.01f);

				if (!Mathf.Approximately(autoControlLength, value))
				{
					autoControlLength = value;
					
					AutoSetAllControlPoints();
					
					NotifyPathModified();
				}
			}
		}

		/// Add new anchor point to end of the path
		public void AddSegmentToEnd(Vector3 anchorPos)
		{
			var lastAnchorIndex = points.Count - 1;
			// Set position for new control to be mirror of its counterpart
			var secondControlForOldLastAnchorOffset = (points[lastAnchorIndex] - points[lastAnchorIndex - 1]);
			if (controlMode != ControlMode.Mirrored && controlMode != ControlMode.Automatic)
			{
				// Set position for new control to be aligned with its counterpart, but with a length of half the distance from prev to new anchor
				var dstPrevToNewAnchor = (points[lastAnchorIndex] - anchorPos).magnitude;
				secondControlForOldLastAnchorOffset = (points[lastAnchorIndex] - points[lastAnchorIndex - 1]).normalized * (dstPrevToNewAnchor * 0.5f);
			}
			var secondControlForOldLastAnchor = points[lastAnchorIndex] + secondControlForOldLastAnchorOffset;
			var controlForNewAnchor = (anchorPos + secondControlForOldLastAnchor) * 0.5f;

			points.Add(secondControlForOldLastAnchor);
			points.Add(controlForNewAnchor);
			points.Add(anchorPos);
			perAnchorNormalsAngle.Add(perAnchorNormalsAngle[^1]);

			if (controlMode == ControlMode.Automatic)
			{
				AutoSetAllAffectedControlPoints(points.Count - 1);
			}

			NotifyPathModified();
		}

		/// Add new anchor point to start of the path
		public void AddSegmentToStart(Vector3 anchorPos)
		{
			// Set position for new control to be mirror of its counterpart
			var secondControlForOldFirstAnchorOffset = (points[0] - points[1]);
			if (controlMode != ControlMode.Mirrored && controlMode != ControlMode.Automatic)
			{
				// Set position for new control to be aligned with its counterpart, but with a length of half the distance from prev to new anchor
				var dstPrevToNewAnchor = (points[0] - anchorPos).magnitude;
				secondControlForOldFirstAnchorOffset = secondControlForOldFirstAnchorOffset.normalized * (dstPrevToNewAnchor * .5f);
			}

			var secondControlForOldFirstAnchor = points[0] + secondControlForOldFirstAnchorOffset;
			var controlForNewAnchor = (anchorPos + secondControlForOldFirstAnchor) * .5f;
			points.Insert(0, anchorPos);
			points.Insert(1, controlForNewAnchor);
			points.Insert(2, secondControlForOldFirstAnchor);
			perAnchorNormalsAngle.Insert(0, perAnchorNormalsAngle[0]);

			if (controlMode == ControlMode.Automatic)
			{
				AutoSetAllAffectedControlPoints(0);
			}
			
			NotifyPathModified();
		}

		/// Insert new anchor point at given position. Automatically place control points around it so as to keep shape of curve the same
		public void SplitSegment(Vector3 anchorPos, int segmentIndex, float splitTime)
		{
			if (float.IsNaN(splitTime))
			{
				Debug.Log("Trying to split segment, but given value was invalid");
				return;
			}

			splitTime = Mathf.Clamp01(splitTime);

			if (controlMode == ControlMode.Automatic)
			{
				points.InsertRange(segmentIndex * 3 + 2, new Vector3[] { Vector3.zero, anchorPos, Vector3.zero });
				AutoSetAllAffectedControlPoints(segmentIndex * 3 + 3);
			}
			else
			{
				// Split the curve to find where control points can be inserted to least affect shape of curve
				// Curve will probably be deformed slightly since splitTime is only an estimate (for performance reasons, and so doesn't correspond exactly with anchorPos)
				var splitSegment = CubicBezierUtility.SplitCurve(GetPointsInSegment(segmentIndex), splitTime);
				points.InsertRange(segmentIndex * 3 + 2, new Vector3[] { splitSegment[0][2], splitSegment[1][0], splitSegment[1][1] });
				var newAnchorIndex = segmentIndex * 3 + 3;
				MovePoint(newAnchorIndex - 2, splitSegment[0][1], true);
				MovePoint(newAnchorIndex + 2, splitSegment[1][2], true);
				MovePoint(newAnchorIndex, anchorPos, true);

				if (controlMode == ControlMode.Mirrored)
				{
					var avgDst = ((splitSegment[0][2] - anchorPos).magnitude + (splitSegment[1][1] - anchorPos).magnitude) / 2;
					MovePoint(newAnchorIndex + 1, anchorPos + (splitSegment[1][1] - anchorPos).normalized * avgDst, true);
				}
			}

			// Insert angle for new anchor (value should be set in between neighbour anchor angles)
			var newAnchorAngleIndex = (segmentIndex + 1) % perAnchorNormalsAngle.Count;
			var anglePrev = perAnchorNormalsAngle[segmentIndex];
			var angleNext = perAnchorNormalsAngle[newAnchorAngleIndex];
			var splitAngle = Mathf.LerpAngle(anglePrev, angleNext, splitTime);
			perAnchorNormalsAngle.Insert(newAnchorAngleIndex, splitAngle);

			NotifyPathModified();
		}

		/// Delete the anchor point at given index, as well as its associated control points
		public void DeleteSegment(int anchorIndex)
		{
			// Don't delete segment if its the last one remaining (or if only two segments in a closed path)
			if (NumSegments > 1)
			{
				if (anchorIndex == 0)
				{
					points.RemoveRange(0, 3);
				}
				else
				{
					if (anchorIndex == points.Count - 1)
					{
						points.RemoveRange(anchorIndex - 2, 3);
					}
					else
					{
						points.RemoveRange(anchorIndex - 1, 3);
					}
				}

				perAnchorNormalsAngle.RemoveAt(anchorIndex / 3);

				if (controlMode == ControlMode.Automatic)
				{
					AutoSetAllControlPoints();
				}

				NotifyPathModified();
			}
		}

		/// Returns an array of the 4 points making up the segment (anchor1, control1, control2, anchor2)
		public Vector3[] GetPointsInSegment(int segmentIndex)
		{
			segmentIndex = Mathf.Clamp(segmentIndex, 0, NumSegments - 1);
			return new Vector3[] { this[segmentIndex * 3], this[segmentIndex * 3 + 1], this[segmentIndex * 3 + 2], this[LoopIndex(segmentIndex * 3 + 3)] };
		}

		/// Move an existing point to a new position
		public void MovePoint(int i, Vector3 pointPos, bool suppressPathModifiedEvent = false)
		{
			var deltaMove = pointPos - points[i];
			var isAnchorPoint = i % 3 == 0;

			// Don't process control point if control mode is set to automatic
			if (isAnchorPoint || controlMode != ControlMode.Automatic)
			{
				points[i] = pointPos;

				if (controlMode == ControlMode.Automatic)
				{
					AutoSetAllAffectedControlPoints(i);
				}
				else
				{
					// Move control points with anchor point
					if (isAnchorPoint)
					{
						if (i + 1 < points.Count)
						{
							points[LoopIndex(i + 1)] += deltaMove;
						}
						if (i - 1 >= 0)
						{
							points[LoopIndex(i - 1)] += deltaMove;
						}
					}
					// If not in free control mode, then move attached control point to be aligned/mirrored (depending on mode)
					else
					{
						if (controlMode != ControlMode.Free)
						{
							var nextPointIsAnchor = (i + 1) % 3 == 0;
							var attachedControlIndex = (nextPointIsAnchor) ? i + 2 : i - 2;
							var anchorIndex = (nextPointIsAnchor) ? i + 1 : i - 1;

							if (attachedControlIndex >= 0 && attachedControlIndex < points.Count)
							{
								var distanceFromAnchor = 0.0f;
								switch (controlMode)
								{
									// If in aligned mode, then attached control's current distance from anchor point should be maintained
									case ControlMode.Aligned:
										distanceFromAnchor = (points[LoopIndex(anchorIndex)] - points[LoopIndex(attachedControlIndex)]).magnitude;
										break;
									case ControlMode.Mirrored:
										distanceFromAnchor = (points[LoopIndex(anchorIndex)] - points[i]).magnitude;
										break;
									case ControlMode.Free:
										break;
									case ControlMode.Automatic:
										break;
									default:
										throw new ArgumentOutOfRangeException();
								}
								var dir = (points[LoopIndex(anchorIndex)] - pointPos).normalized;
								points[LoopIndex(attachedControlIndex)] = points[LoopIndex(anchorIndex)] + dir * distanceFromAnchor;
							}
						}
					}
				}

				if (!suppressPathModifiedEvent)
				{
					NotifyPathModified();
				}
			}
		}

		/// Update the bounding box of the path
		public Bounds CalculateBoundsWithTransform(Transform transform)
		{
			// Loop through all segments and keep track of the minmax points of all their bounding boxes
			var minMax = new MinMax3D();

			for (var i = 0; i < NumSegments; i++)
			{
				var p = GetPointsInSegment(i);
				for (var j = 0; j < p.Length; j++)
				{
					p[j] = MathUtility.TransformPoint(p[j], transform);
				}

				minMax.AddValue(p[0]);
				minMax.AddValue(p[3]);

				var extremePointTimes = CubicBezierUtility.ExtremePointTimes(p[0], p[1], p[2], p[3]);
				foreach (var t in extremePointTimes)
				{
					minMax.AddValue(CubicBezierUtility.EvaluateCurve(p, t));
				}
			}

			return new Bounds((minMax.Min + minMax.Max) / 2, minMax.Max - minMax.Min);
		}

		/// Flip the normal vectors 180 degrees
		public bool AreNormalsFlipped
		{
			get => areNormalsFlipped;
			
			set
			{
				if (areNormalsFlipped != value)
				{
					areNormalsFlipped = value;
				
					NotifyPathModified();
				}
			}
		}

		/// Global angle that all normal vectors are rotated by (only relevant for paths in 3D space)
		public float GlobalNormalsAngle
		{
			get => globalNormalsAngle;
			
			set
			{
				if (!Mathf.Approximately(globalNormalsAngle, value))
				{
					globalNormalsAngle = value;
				
					NotifyPathModified();
				}
			}
		}

		/// Get the desired angle of the normal vector at a particular anchor (only relevant for paths in 3D space)
		public float GetAnchorNormalAngle(int anchorIndex)
		{
			return perAnchorNormalsAngle[anchorIndex] % 360;
		}

		/// Set the desired angle of the normal vector at a particular anchor (only relevant for paths in 3D space)
		public void SetAnchorNormalAngle(int anchorIndex, float angle)
		{
			angle = (angle + 360) % 360;

			if (!Mathf.Approximately(perAnchorNormalsAngle[anchorIndex], angle))
			{
				perAnchorNormalsAngle[anchorIndex] = angle;
			
				NotifyPathModified();
			}
		}

		/// Reset global and anchor normal angles to 0
		public void ResetNormalAngles()
		{
			for (var i = 0; i < perAnchorNormalsAngle.Count; i++)
			{
				perAnchorNormalsAngle[i] = 0;
			}
			
			globalNormalsAngle = 0;
			
			NotifyPathModified();
		}

		#endregion

		
		#region Internal methods and accessors

		/// Update the bounding box of the path
		private void UpdateBounds()
		{
			if (areBoundsUpToDated)
			{
				return;
			}

			// Loop through all segments and keep track of the minmax points of all their bounding boxes
			var minMax = new MinMax3D();

			for (var i = 0; i < NumSegments; i++)
			{
				var p = GetPointsInSegment(i);
				minMax.AddValue(p[0]);
				minMax.AddValue(p[3]);

				var extremePointTimes = CubicBezierUtility.ExtremePointTimes(p[0], p[1], p[2], p[3]);
				foreach (var t in extremePointTimes)
				{
					minMax.AddValue(CubicBezierUtility.EvaluateCurve(p, t));
				}
			}

			areBoundsUpToDated = true;
			new Bounds((minMax.Min + minMax.Max) / 2, minMax.Max - minMax.Min);
		}

		/// Determines good positions (for a smooth path) for the control points affected by a moved/inserted anchor point
		private void AutoSetAllAffectedControlPoints(int updatedAnchorIndex)
		{
			for (var i = updatedAnchorIndex - 3; i <= updatedAnchorIndex + 3; i += 3)
			{
				if (i >= 0 && i < points.Count)
				{
					AutoSetAnchorControlPoints(LoopIndex(i));
				}
			}

			AutoSetStartAndEndControls();
		}

		/// Determines good positions (for a smooth path) for all control points
		private void AutoSetAllControlPoints()
		{
			if (NumAnchorPoints > 2)
			{
				for (var i = 0; i < points.Count; i += 3)
				{
					AutoSetAnchorControlPoints(i);
				}
			}

			AutoSetStartAndEndControls();
		}

		/// Calculates good positions (to result in smooth path) for the controls around specified anchor
		private void AutoSetAnchorControlPoints(int anchorIndex)
		{
			// Calculate a vector that is perpendicular to the vector bisecting the angle between this anchor and its two immediate neighbours
			// The control points will be placed along that vector
			var anchorPos = points[anchorIndex];
			var dir = Vector3.zero;
			var neighbourDistances = new float[2];

			if (anchorIndex - 3 >= 0)
			{
				var offset = points[LoopIndex(anchorIndex - 3)] - anchorPos;
				dir += offset.normalized;
				neighbourDistances[0] = offset.magnitude;
			}
			if (anchorIndex + 3 >= 0)
			{
				var offset = points[LoopIndex(anchorIndex + 3)] - anchorPos;
				dir -= offset.normalized;
				neighbourDistances[1] = -offset.magnitude;
			}

			dir.Normalize();

			// Set the control points along the calculated direction, with a distance proportional to the distance to the neighbouring control point
			for (var i = 0; i < 2; i++)
			{
				var controlIndex = anchorIndex + i * 2 - 1;
				if (controlIndex >= 0 && controlIndex < points.Count)
				{
					points[LoopIndex(controlIndex)] = anchorPos + dir * (neighbourDistances[i] * autoControlLength);
				}
			}
		}

		/// Determines good positions (for a smooth path) for the control points at the start and end of a path
		private void AutoSetStartAndEndControls()
		{
			// Handle case with 2 anchor points separately, as otherwise minor adjustments cause path to constantly flip
			if (NumAnchorPoints == 2)
			{
				points[1] = points[0] + (points[3] - points[0]) * 0.25f;
				points[2] = points[3] + (points[0] - points[3]) * 0.25f;
			}
			else
			{
				points[1] = (points[0] + points[2]) * 0.5f;
				points[^2] = (points[^1] + points[^3]) * 0.5f;
			}
		}

		/// Add/remove the extra 2 controls required for a closed path
		private void UpdateClosedState()
		{
			points.RemoveRange(points.Count - 2, 2);

			if (controlMode == ControlMode.Automatic)
			{
				AutoSetStartAndEndControls();
			}

			if (OnModified != null)
			{
				OnModified();
			}
		}

		/// Loop index around to start/end of points array if out of bounds (useful when working with closed paths)
		private int LoopIndex(int i)
		{
			return (i + points.Count) % points.Count;
		}

		// Called when the path is modified
		public void NotifyPathModified()
		{
			areBoundsUpToDated = false;
			
			if (OnModified != null)
			{
				OnModified();
			}
		}

		#endregion
	}
}