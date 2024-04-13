using System.Collections.Generic;
using Resources.PathCreator.Core.Runtime.Objects;
using Resources.PathCreator.Core.Runtime.Utility;
using UnityEditor;
using UnityEngine;

namespace Resources.PathCreator.Core.Editor.Helper
{
	public class ScreenSpacePolyLine
	{
		private const int AccuracyMultiplier = 10;
		// dont allow vertices to be spaced too far apart, as screen space-world space conversion can then be noticeably off
		private const float IntermediaryThreshold = 0.2f;

		private readonly List<Vector3> _verticesWorld;
		// For each point in the polyline, says which bezier segment it belongs to
		private readonly List<int> _vertexToPathSegmentMap;
		// Stores the index in the vertices list where the start point of each segment is
		private readonly int[] _segmentStartIndices;

		private readonly float _pathLengthWorld;
		private readonly float[] _cumulativeLengthWorld;

		private Vector2[] _points;

		private Vector3 _prevCamPos;
		private Quaternion _prevCamRot;
		private bool _prevCamIsOrtho;

		private readonly Transform _transform;
		private readonly Vector3 _transformPosition;
		private readonly Quaternion _transformRotation;
		private readonly Vector3 _transformScale;

		public ScreenSpacePolyLine(BezierPath bezierPath, Transform transform, float maxAngleError, float minVertexDst, float accuracy = 1)
		{
			_transform = transform;
			_transformPosition = transform.position;
			_transformRotation = transform.rotation;
			_transformScale = transform.localScale;

			// Split path in vertices based on angle error
			_verticesWorld = new List<Vector3>();
			_vertexToPathSegmentMap = new List<int>();
			_segmentStartIndices = new int[bezierPath.NumSegments + 1];

			_verticesWorld.Add(bezierPath[0]);
			_vertexToPathSegmentMap.Add(0);

			for (var segmentIndex = 0; segmentIndex < bezierPath.NumSegments; segmentIndex++)
			{
				var segmentPoints = bezierPath.GetPointsInSegment(segmentIndex);
				_verticesWorld.Add(segmentPoints[0]);
				_vertexToPathSegmentMap.Add(segmentIndex);
				_segmentStartIndices[segmentIndex] = _verticesWorld.Count - 1;

				var prevPointOnPath = segmentPoints[0];
				var lastAddedPoint = prevPointOnPath;
				float dstSinceLastVertex = 0;
				float dstSinceLastIntermediary = 0;

				var estimatedSegmentLength = CubicBezierUtility.EstimateCurveLength(segmentPoints[0], segmentPoints[1], segmentPoints[2], segmentPoints[3]);
				var divisions = Mathf.CeilToInt(estimatedSegmentLength * accuracy * AccuracyMultiplier);
				var increment = 1f / divisions;

				for (var t = increment; t <= 1; t += increment)
				{
					var pointOnPath = CubicBezierUtility.EvaluateCurve(segmentPoints[0], segmentPoints[1], segmentPoints[2], segmentPoints[3], t);
					var nextPointOnPath = CubicBezierUtility.EvaluateCurve(segmentPoints[0], segmentPoints[1], segmentPoints[2], segmentPoints[3], t + increment);

					// angle at current point on path
					var localAngle = 180 - MathUtility.MinAngle(prevPointOnPath, pointOnPath, nextPointOnPath);
					// angle between the last added vertex, the current point on the path, and the next point on the path
					var angleFromPrevVertex = 180 - MathUtility.MinAngle(lastAddedPoint, pointOnPath, nextPointOnPath);
					var angleError = Mathf.Max(localAngle, angleFromPrevVertex);

					if (angleError > maxAngleError && dstSinceLastVertex >= minVertexDst)
					{
						dstSinceLastVertex = 0;
						dstSinceLastIntermediary = 0;
						_verticesWorld.Add(pointOnPath);
						_vertexToPathSegmentMap.Add(segmentIndex);
						lastAddedPoint = pointOnPath;
					}
					else
					{
						if (dstSinceLastIntermediary > IntermediaryThreshold)
						{
							_verticesWorld.Add(pointOnPath);
							_vertexToPathSegmentMap.Add(segmentIndex);
							dstSinceLastIntermediary = 0;
						}
						else
						{
							dstSinceLastIntermediary += (pointOnPath - prevPointOnPath).magnitude;
						}
						dstSinceLastVertex += (pointOnPath - prevPointOnPath).magnitude;
					}
					prevPointOnPath = pointOnPath;
				}
			}

			_segmentStartIndices[bezierPath.NumSegments] = _verticesWorld.Count;

			// ensure final point gets added (unless path is closed loop)
			_verticesWorld.Add(bezierPath[bezierPath.NumPoints - 1]);

			// Calculate length
			_cumulativeLengthWorld = new float[_verticesWorld.Count];
			for (var i = 0; i < _verticesWorld.Count; i++)
			{
				_verticesWorld[i] = MathUtility.TransformPoint(_verticesWorld[i], transform);
				if (i > 0)
				{
					_pathLengthWorld += (_verticesWorld[i - 1] - _verticesWorld[i]).magnitude;
					_cumulativeLengthWorld[i] = _pathLengthWorld;
				}
			}
		}

		private void ComputeScreenSpace()
		{
			if (Camera.current.transform.position != _prevCamPos || Camera.current.transform.rotation != _prevCamRot || Camera.current.orthographic != _prevCamIsOrtho)
			{
				_points = new Vector2[_verticesWorld.Count];
				for (var i = 0; i < _verticesWorld.Count; i++)
				{
					_points[i] = HandleUtility.WorldToGUIPoint(_verticesWorld[i]);
				}

				var current = Camera.current;
				var transform = current.transform;
				_prevCamPos = transform.position;
				_prevCamRot = transform.rotation;
				_prevCamIsOrtho = current.orthographic;
			}
		}

		public MouseInfo CalculateMouseInfo()
		{
			ComputeScreenSpace();

			var mousePos = Event.current.mousePosition;
			var minDst = float.MaxValue;
			var closestPolyLineSegmentIndex = 0;
			var closestBezierSegmentIndex = 0;

			for (var i = 0; i < _points.Length - 1; i++)
			{
				var dst = HandleUtility.DistancePointToLineSegment(mousePos, _points[i], _points[i + 1]);

				if (dst < minDst)
				{
					minDst = dst;
					closestPolyLineSegmentIndex = i;
					closestBezierSegmentIndex = _vertexToPathSegmentMap[i];
				}
			}

			var closestPointOnLine = MathUtility.ClosestPointOnLineSegment(mousePos, _points[closestPolyLineSegmentIndex], _points[closestPolyLineSegmentIndex + 1]);
			var dstToPointOnLine = (_points[closestPolyLineSegmentIndex] - closestPointOnLine).magnitude;

			var d = (_points[closestPolyLineSegmentIndex] - _points[closestPolyLineSegmentIndex + 1]).magnitude;
			var percentBetweenVertices = (d == 0) ? 0 : dstToPointOnLine / d;
			var closestPoint3D = Vector3.Lerp(_verticesWorld[closestPolyLineSegmentIndex], _verticesWorld[closestPolyLineSegmentIndex + 1], percentBetweenVertices);

			var distanceAlongPathWorld = _cumulativeLengthWorld[closestPolyLineSegmentIndex] + Vector3.Distance(_verticesWorld[closestPolyLineSegmentIndex], closestPoint3D);
			var timeAlongPath = distanceAlongPathWorld / _pathLengthWorld;

			// Calculate how far between the current bezier segment the closest point on the line is

			var bezierSegmentStartIndex = _segmentStartIndices[closestBezierSegmentIndex];
			var bezierSegmentEndIndex = _segmentStartIndices[closestBezierSegmentIndex + 1];
			var bezierSegmentLength = _cumulativeLengthWorld[bezierSegmentEndIndex] - _cumulativeLengthWorld[bezierSegmentStartIndex];
			var distanceAlongBezierSegment = distanceAlongPathWorld - _cumulativeLengthWorld[bezierSegmentStartIndex];
			var timeAlongBezierSegment = distanceAlongBezierSegment / bezierSegmentLength;

			return new MouseInfo(minDst, closestPoint3D, distanceAlongPathWorld, timeAlongPath, timeAlongBezierSegment, closestBezierSegmentIndex);
		}

		public bool TransformIsOutOfDate()
		{
			return _transform.position != _transformPosition || _transform.rotation != _transformRotation || _transform.localScale != _transformScale;
		}

		public struct MouseInfo
		{
			public readonly float mouseDstToLine;
			public readonly Vector3 closestWorldPointToMouse;
			public readonly float distanceAlongPathWorld;
			public readonly float timeOnPath;
			public readonly float timeOnBezierSegment;
			public readonly int closestSegmentIndex;

			public MouseInfo(float mouseDstToLine, Vector3 closestWorldPointToMouse, float distanceAlongPathWorld, float timeOnPath, float timeOnBezierSegment, int closestSegmentIndex)
			{
				this.mouseDstToLine = mouseDstToLine;
				this.closestWorldPointToMouse = closestWorldPointToMouse;
				this.distanceAlongPathWorld = distanceAlongPathWorld;
				this.timeOnPath = timeOnPath;
				this.timeOnBezierSegment = timeOnBezierSegment;
				this.closestSegmentIndex = closestSegmentIndex;
			}
		}
	}
}