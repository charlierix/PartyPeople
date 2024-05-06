using Game.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.Mathematics
{
    #region class: Triangle_wpf

    //TODO:  Add methods like Clone, GetTransformed(transform), etc

    /// <summary>
    /// This stores 3 points explicitly - as opposed to TriangleIndexed, which stores ints that point to a list of points
    /// </summary>
    public class Triangle_wpf : ITriangle_wpf
    {
        #region Declaration Section

        // Caching these so that Enum.GetValues doesn't have to be called all over the place
        public static TriangleEdge[] Edges = (TriangleEdge[])Enum.GetValues(typeof(TriangleEdge));
        public static TriangleCorner[] Corners = (TriangleCorner[])Enum.GetValues(typeof(TriangleCorner));

        #endregion

        #region Constructor

        public Triangle_wpf()
        {
        }

        public Triangle_wpf(Point3D point0, Point3D point1, Point3D point2)
        {
            //TODO:  If the property sets have more added to them in the future, do that in this constructor as well
            //Point0 = point0;
            //Point1 = point1;
            //Point2 = point2;

            _point0 = point0;
            _point1 = point1;
            _point2 = point2;

            OnPointChanged();
        }
        public Triangle_wpf(Vector3D normal, Point3D pointOnPlane)
        {
            Vector3D dir1 = Math3D.GetArbitraryOrthogonal(normal);
            Vector3D dir2 = Vector3D.CrossProduct(dir1, normal);

            _point0 = pointOnPlane + dir1;
            _point1 = pointOnPlane;
            _point2 = pointOnPlane + dir2;

            OnPointChanged();
        }

        #endregion

        #region ITriangle Members

        private Point3D? _point0 = null;
        public Point3D Point0
        {
            get
            {
                return _point0.Value;       // skipping the null check to be as fast as possible (.net will throw an execption anyway)
            }
            set
            {
                _point0 = value;
                OnPointChanged();
            }
        }

        private Point3D? _point1 = null;
        public Point3D Point1
        {
            get
            {
                return _point1.Value;       // skipping the null check to be as fast as possible (.net will throw an execption anyway)
            }
            set
            {
                _point1 = value;
                OnPointChanged();
            }
        }

        private Point3D? _point2 = null;
        public Point3D Point2
        {
            get
            {
                return _point2.Value;       // skipping the null check to be as fast as possible (.net will throw an execption anyway)
            }
            set
            {
                _point2 = value;
                OnPointChanged();
            }
        }

        public Point3D[] PointArray
        {
            get
            {
                return [_point0.Value, _point1.Value, _point2.Value];
            }
        }

        public Point3D this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return Point0;

                    case 1:
                        return Point1;

                    case 2:
                        return Point2;

                    default:
                        throw new ArgumentOutOfRangeException("index", $"index can only be 0, 1, 2: {index}");
                }
            }
            set
            {
                switch (index)
                {
                    case 0:
                        Point0 = value;
                        break;

                    case 1:
                        Point1 = value;
                        break;

                    case 2:
                        Point2 = value;
                        break;

                    default:
                        throw new ArgumentOutOfRangeException("index", $"index can only be 0, 1, 2: {index}");
                }
            }
        }

        private Vector3D? _normal = null;
        /// <summary>
        /// This returns the triangle's normal.  Its length is the area of the triangle
        /// </summary>
        public Vector3D Normal
        {
            get
            {
                if (_normal == null)
                {
                    CalculateNormal(out Vector3D normal, out double length, out Vector3D normalUnit, Point0, Point1, Point2);

                    _normal = normal;
                    _normalLength = length;
                    _normalUnit = normalUnit;
                }

                return _normal.Value;
            }
        }
        private Vector3D? _normalUnit = null;
        /// <summary>
        /// This returns the triangle's normal.  Its length is one
        /// </summary>
        public Vector3D NormalUnit
        {
            get
            {
                if (_normalUnit == null)
                {
                    CalculateNormal(out Vector3D normal, out double length, out Vector3D normalUnit, Point0, Point1, Point2);

                    _normal = normal;
                    _normalLength = length;
                    _normalUnit = normalUnit;
                }

                return _normalUnit.Value;
            }
        }
        private double? _normalLength = null;
        /// <summary>
        /// This returns the length of the normal (the area of the triangle)
        /// NOTE:  Call this if you just want to know the length of the normal, it's cheaper than calling Normal.Length, since it's already been calculated
        /// </summary>
        public double NormalLength
        {
            get
            {
                if (_normalLength == null)
                {
                    CalculateNormal(out Vector3D normal, out double length, out Vector3D normalUnit, Point0, Point1, Point2);

                    _normal = normal;
                    _normalLength = length;
                    _normalUnit = normalUnit;
                }

                return _normalLength.Value;
            }
        }

        private double? _planeDistance = null;
        public double PlaneDistance
        {
            get
            {
                if (_planeDistance == null)
                    _planeDistance = Math3D.GetPlaneOriginDistance(NormalUnit, Point0);

                return _planeDistance.Value;
            }
        }

        private long? _token = null;
        public long Token
        {
            get
            {
                if (_token == null)
                    _token = TokenGenerator.NextToken();

                return _token.Value;
            }
        }

        public Point3D GetCenterPoint()
        {
            return GetCenterPoint(Point0, Point1, Point2);
        }
        public Point3D GetPoint(TriangleEdge edge, bool isFrom)
        {
            return GetPoint(this, edge, isFrom);
        }
        public Point3D GetCommonPoint(TriangleEdge edge0, TriangleEdge edge1)
        {
            return GetCommonPoint(this, edge0, edge1);
        }
        public Point3D GetUncommonPoint(TriangleEdge edge0, TriangleEdge edge1)
        {
            return GetUncommonPoint(this, edge0, edge1);
        }
        public Point3D GetOppositePoint(TriangleEdge edge)
        {
            return GetOppositePoint(this, edge);
        }
        public Point3D GetEdgeMidpoint(TriangleEdge edge)
        {
            return GetEdgeMidpoint(this, edge);
        }
        public double GetEdgeLength(TriangleEdge edge)
        {
            return GetEdgeLength(this, edge);
        }

        #endregion
        #region IComparable<ITriangle> Members

        /// <summary>
        /// This is so triangles can be used as keys in a sorted list
        /// </summary>
        public int CompareTo(ITriangle_wpf other)
        {
            if (other == null)
                // this is greater than null
                return 1;

            if (Token < other.Token)
                return -1;

            else if (Token > other.Token)
                return 1;

            else
                return 0;
        }

        #endregion

        #region Public Methods

        public static Point3D[] GetUniquePoints(IEnumerable<ITriangle_wpf> triangles)
        {
            return triangles.
                SelectMany(o => o.PointArray).
                Distinct((p1, p2) => Math3D.IsNearValue(p1, p2)).
                ToArray();
        }

        /// <summary>
        /// This helps a lot when looking at lists of triangles in the quick watch
        /// </summary>
        public override string ToString()
        {
            return string.Format("({0}) ({1}) ({2})",
                _point0 == null ? "null" : _point0.Value.ToString(2),
                _point1 == null ? "null" : _point1.Value.ToString(2),
                _point2 == null ? "null" : _point2.Value.ToString(2));
        }

        #endregion
        #region Internal Methods

        internal static void CalculateNormal(out Vector3D normal, out double normalLength, out Vector3D normalUnit, Point3D point0, Point3D point1, Point3D point2)
        {
            Vector3D dir1 = point0 - point1;
            Vector3D dir2 = point2 - point1;

            Vector3D triangleNormal = Vector3D.CrossProduct(dir2, dir1);

            normal = triangleNormal;
            normalLength = triangleNormal.Length;
            normalUnit = triangleNormal / normalLength;
        }

        internal static Point3D GetCenterPoint(Point3D point0, Point3D point1, Point3D point2)
        {
            //return ((triangle.Point0.ToVector() + triangle.Point1.ToVector() + triangle.Point2.ToVector()) / 3d).ToPoint();

            // Doing the math with doubles to avoid casting to vector
            double x = (point0.X + point1.X + point2.X) / 3d;
            double y = (point0.Y + point1.Y + point2.Y) / 3d;
            double z = (point0.Z + point1.Z + point2.Z) / 3d;

            return new Point3D(x, y, z);
        }

        internal static Point3D GetPoint(ITriangle_wpf triangle, TriangleEdge edge, bool isFrom)
        {
            switch (edge)
            {
                case TriangleEdge.Edge_01:
                    return isFrom ? triangle.Point0 : triangle.Point1;

                case TriangleEdge.Edge_12:
                    return isFrom ? triangle.Point1 : triangle.Point2;

                case TriangleEdge.Edge_20:
                    return isFrom ? triangle.Point2 : triangle.Point0;

                default:
                    throw new ApplicationException($"Unknown TriangleEdge: {edge}");
            }
        }

        internal static Point3D GetCommonPoint(ITriangle_wpf triangle, TriangleEdge edge0, TriangleEdge edge1)
        {
            Point3D[] points0 = [triangle.GetPoint(edge0, true), triangle.GetPoint(edge0, false)];
            Point3D[] points1 = [triangle.GetPoint(edge1, true), triangle.GetPoint(edge1, false)];

            // Find exact
            for (int i = 0; i < points0.Length; i++)
                for (int j = 0; j < points1.Length; j++)
                    if (points0[i] == points1[j])
                        return points0[i];

            // Find close - execution should never get here, just being safe
            for (int i = 0; i < points0.Length; i++)
                for (int j = 0; j < points1.Length; j++)
                    if (Math3D.IsNearValue(points0[i], points1[j]))
                        return points0[i];

            throw new ApplicationException("Didn't find a common point");
        }

        internal static Point3D GetUncommonPoint(ITriangle_wpf triangle, TriangleEdge edge0, TriangleEdge edge1)
        {
            Point3D[] points0 = [triangle.GetPoint(edge0, true), triangle.GetPoint(edge0, false)];
            Point3D[] points1 = [triangle.GetPoint(edge1, true), triangle.GetPoint(edge1, false)];

            // Find exact
            for (int i = 0; i < points0.Length; i++)
                for (int j = 0; j < points1.Length; j++)
                    if (points0[i] == points1[j])
                        return points0[i == 0 ? 1 : 0];     // return the one that isn't common

            // Find close - execution should never get here, just being safe
            for (int i = 0; i < points0.Length; i++)
                for (int j = 0; j < points1.Length; j++)
                    if (Math3D.IsNearValue(points0[i], points1[j]))
                        return points0[i == 0 ? 1 : 0];     // return the one that isn't common

            throw new ApplicationException("Didn't find a common point");
        }

        internal static Point3D GetOppositePoint(ITriangle_wpf triangle, TriangleEdge edge)
        {
            switch (edge)
            {
                case TriangleEdge.Edge_01:
                    return triangle.Point2;

                case TriangleEdge.Edge_12:
                    return triangle.Point0;

                case TriangleEdge.Edge_20:
                    return triangle.Point1;

                default:
                    throw new ApplicationException("Unknown TriangleEdge: " + edge.ToString());
            }
        }

        internal static Point3D GetEdgeMidpoint(ITriangle_wpf triangle, TriangleEdge edge)
        {
            Point3D point0 = triangle.GetPoint(edge, true);
            Point3D point1 = triangle.GetPoint(edge, false);

            Vector3D halfLength = (point1 - point0) * .5d;

            return point0 + halfLength;
        }

        internal static double GetEdgeLength(ITriangle_wpf triangle, TriangleEdge edge)
        {
            Point3D point0 = triangle.GetPoint(edge, true);
            Point3D point1 = triangle.GetPoint(edge, false);

            return (point1 - point0).Length;
        }

        #endregion
        #region Protected Methods

        protected virtual void OnPointChanged()
        {
            _normal = null;
            _normalUnit = null;
            _normalLength = null;
            _planeDistance = null;
        }

        #endregion
    }

    #endregion
    #region class: TriangleThreadsafe_wpf

    /// <summary>
    /// This is a copy of Triangle, but is readonly
    /// NOTE: Only use this class if it's needed.  Extra stuff needs to be cached during the constructor, even if it will never be used, so this class is a bit more expensive
    /// </summary>
    public class TriangleThreadsafe_wpf : ITriangle_wpf
    {
        #region Constructor

        public TriangleThreadsafe_wpf(Point3D point0, Point3D point1, Point3D point2, bool calculateNormalUpFront)
        {
            _point0 = point0;
            _point1 = point1;
            _point2 = point2;

            if (calculateNormalUpFront)
            {
                Triangle_wpf.CalculateNormal(out Vector3D normal, out double length, out Vector3D normalUnit, point0, point1, point2);

                _normal = normal;
                _normalLength = length;
                _normalUnit = normalUnit;

                _planeDistance = Math3D.GetPlaneOriginDistance(normalUnit, point0);
            }
            else
            {
                _normal = null;
                _normalLength = null;
                _normalUnit = null;
                _planeDistance = null;
            }

            _token = TokenGenerator.NextToken();
        }

        #endregion

        #region ITriangle Members

        private readonly Point3D _point0;
        public Point3D Point0 => _point0;

        private readonly Point3D _point1;
        public Point3D Point1 => _point1;

        private readonly Point3D _point2;
        public Point3D Point2 => _point2;

        public Point3D[] PointArray => [_point0, _point1, _point2];

        public Point3D this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return Point0;

                    case 1:
                        return Point1;

                    case 2:
                        return Point2;

                    default:
                        throw new ArgumentOutOfRangeException("index", $"index can only be 0, 1, 2: {index}");
                }
            }
        }

        private readonly Vector3D? _normal;
        /// <summary>
        /// This returns the triangle's normal.  Its length is the area of the triangle
        /// </summary>
        public Vector3D Normal
        {
            get
            {
                return _normal == null ?
                    Vector3D.CrossProduct(_point2 - _point1, _point0 - _point1) :
                    _normal.Value;
            }
        }
        private readonly Vector3D? _normalUnit;
        /// <summary>
        /// This returns the triangle's normal.  Its length is one
        /// </summary>
        public Vector3D NormalUnit
        {
            get
            {
                if (_normalUnit == null)
                {
                    Triangle_wpf.CalculateNormal(out _, out _, out Vector3D normalUnit, _point0, _point1, _point2);
                    return normalUnit;
                }
                else
                {
                    return _normalUnit.Value;
                }
            }
        }
        private readonly double? _normalLength;
        /// <summary>
        /// This returns the length of the normal (the area of the triangle)
        /// NOTE:  Call this if you just want to know the length of the normal, it's cheaper than calling Normal.Length, since it's already been calculated
        /// </summary>
        public double NormalLength
        {
            get
            {
                if (_normalLength == null)
                {
                    Triangle_wpf.CalculateNormal(out _, out double length, out _, _point0, _point1, _point2);
                    return length;
                }
                else
                {
                    return _normalLength.Value;
                }
            }
        }

        private readonly double? _planeDistance;
        public double PlaneDistance
        {
            get
            {
                return _planeDistance == null ?
                    Math3D.GetPlaneOriginDistance(NormalUnit, _point0) :
                    _planeDistance.Value;
            }
        }

        private readonly long _token;
        public long Token => _token;

        public Point3D GetCenterPoint()
        {
            return Triangle_wpf.GetCenterPoint(_point0, _point1, _point2);
        }
        public Point3D GetPoint(TriangleEdge edge, bool isFrom)
        {
            return Triangle_wpf.GetPoint(this, edge, isFrom);
        }
        public Point3D GetCommonPoint(TriangleEdge edge0, TriangleEdge edge1)
        {
            return Triangle_wpf.GetCommonPoint(this, edge0, edge1);
        }
        public Point3D GetUncommonPoint(TriangleEdge edge0, TriangleEdge edge1)
        {
            return Triangle_wpf.GetUncommonPoint(this, edge0, edge1);
        }
        public Point3D GetOppositePoint(TriangleEdge edge)
        {
            return Triangle_wpf.GetOppositePoint(this, edge);
        }
        public Point3D GetEdgeMidpoint(TriangleEdge edge)
        {
            return Triangle_wpf.GetEdgeMidpoint(this, edge);
        }
        public double GetEdgeLength(TriangleEdge edge)
        {
            return Triangle_wpf.GetEdgeLength(this, edge);
        }

        #endregion
        #region IComparable<ITriangle> Members

        /// <summary>
        /// I wanted to be able to use triangles as keys in a sorted list
        /// </summary>
        public int CompareTo(ITriangle_wpf other)
        {
            if (other == null)
                // I'm greater than null
                return 1;

            if (Token < other.Token)
                return -1;

            else if (Token > other.Token)
                return 1;

            else
                return 0;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// This helps a lot when looking at lists of triangles in the quick watch
        /// </summary>
        public override string ToString()
        {
            return $"({_point0}) ({_point1}) ({_point2})";
        }

        #endregion
    }

    #endregion

    // These two are now threadsafe
    #region class: TriangleIndexed_wpf

    /// <summary>
    /// This takes an array of points, then holds three ints that point to three of those points
    /// </summary>
    public class TriangleIndexed_wpf : ITriangleIndexed_wpf
    {
        #region Declaration Section

        // I was going to use RWLS, but it is 1.7 times slower than standard lock, so it would take quite a few simultaneous reads to
        // justify using
        //http://blogs.msdn.com/b/pedram/archive/2007/10/07/a-performance-comparison-of-readerwriterlockslim-with-readerwriterlock.aspx
        //private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        private readonly object _lock = new object();

        #endregion

        #region Constructor

        public TriangleIndexed_wpf(int index0, int index1, int index2, Point3D[] allPoints)
        {
            _index0 = index0;
            _index1 = index1;
            _index2 = index2;
            _allPoints = allPoints;
            OnPointChanged();
        }

        #endregion

        #region ITriangle Members

        public Point3D Point0 => _allPoints[_index0];
        public Point3D Point1 => _allPoints[_index1];
        public Point3D Point2 => _allPoints[_index2];

        private Point3D[] _pointArray;
        public Point3D[] PointArray
        {
            get
            {
                lock (_lock)
                {
                    if (_pointArray == null)
                        _pointArray = [Point0, Point1, Point2];

                    return _pointArray;
                }
            }
        }

        public Point3D this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return _allPoints[_index0];

                    case 1:
                        return _allPoints[_index1];

                    case 2:
                        return _allPoints[_index2];

                    default:
                        throw new ArgumentOutOfRangeException("index", $"index can only be 0, 1, 2: {index}");
                }
            }
        }

        private Vector3D? _normal = null;
        /// <summary>
        /// This returns the triangle's normal.  Its length is the area of the triangle
        /// </summary>
        public Vector3D Normal
        {
            get
            {
                lock (_lock)
                {
                    if (_normal == null)
                    {
                        Triangle_wpf.CalculateNormal(out Vector3D normal, out double length, out Vector3D normalUnit, Point0, Point1, Point2);

                        _normal = normal;
                        _normalLength = length;
                        _normalUnit = normalUnit;
                    }

                    return _normal.Value;
                }
            }
        }
        private Vector3D? _normalUnit = null;
        /// <summary>
        /// This returns the triangle's normal.  Its length is one
        /// </summary>
        public Vector3D NormalUnit
        {
            get
            {
                lock (_lock)
                {
                    if (_normalUnit == null)
                    {
                        Triangle_wpf.CalculateNormal(out Vector3D normal, out double length, out Vector3D normalUnit, Point0, Point1, Point2);

                        _normal = normal;
                        _normalLength = length;
                        _normalUnit = normalUnit;
                    }

                    return _normalUnit.Value;
                }
            }
        }
        private double? _normalLength = null;
        /// <summary>
        /// This returns the length of the normal (the area of the triangle)
        /// NOTE:  Call this if you just want to know the length of the normal, it's cheaper than calling Normal.Length, since it's already been calculated
        /// </summary>
        public double NormalLength
        {
            get
            {
                lock (_lock)
                {
                    if (_normalLength == null)
                    {
                        Triangle_wpf.CalculateNormal(out Vector3D normal, out double length, out Vector3D normalUnit, Point0, Point1, Point2);

                        _normal = normal;
                        _normalLength = length;
                        _normalUnit = normalUnit;
                    }

                    return _normalLength.Value;
                }
            }
        }

        private double? _planeDistance = null;
        public double PlaneDistance
        {
            get
            {
                lock (_lock)
                {
                    if (_planeDistance == null)
                    {
                        if (_normalUnit == null)
                        {
                            Triangle_wpf.CalculateNormal(out Vector3D normal, out double length, out Vector3D normalUnit, Point0, Point1, Point2);

                            _normal = normal;
                            _normalLength = length;
                            _normalUnit = normalUnit;
                        }

                        _planeDistance = Math3D.GetPlaneOriginDistance(_normalUnit.Value, Point0);
                    }

                    return _planeDistance.Value;
                }
            }
        }

        private long? _token = null;
        public long Token
        {
            get
            {
                lock (_lock)
                {
                    if (_token == null)
                        _token = TokenGenerator.NextToken();

                    return _token.Value;
                }
            }
        }

        public Point3D GetCenterPoint()
        {
            return Triangle_wpf.GetCenterPoint(Point0, Point1, Point2);
        }
        public Point3D GetPoint(TriangleEdge edge, bool isFrom)
        {
            return Triangle_wpf.GetPoint(this, edge, isFrom);
        }
        public Point3D GetCommonPoint(TriangleEdge edge0, TriangleEdge edge1)
        {
            return Triangle_wpf.GetCommonPoint(this, edge0, edge1);
        }
        public Point3D GetUncommonPoint(TriangleEdge edge0, TriangleEdge edge1)
        {
            return Triangle_wpf.GetUncommonPoint(this, edge0, edge1);
        }
        public Point3D GetOppositePoint(TriangleEdge edge)
        {
            return Triangle_wpf.GetOppositePoint(this, edge);
        }
        public Point3D GetEdgeMidpoint(TriangleEdge edge)
        {
            return Triangle_wpf.GetEdgeMidpoint(this, edge);
        }
        public double GetEdgeLength(TriangleEdge edge)
        {
            return Triangle_wpf.GetEdgeLength(this, edge);
        }

        #endregion
        #region ITriangleIndexed Members

        private readonly int _index0;
        public int Index0 => _index0;

        private readonly int _index1;
        public int Index1 => _index1;

        private readonly int _index2;
        public int Index2 => _index2;

        private readonly Point3D[] _allPoints;
        public Point3D[] AllPoints => _allPoints;

        private int[] _indexArray = null;
        /// <summary>
        /// This returns an array (element 0 is Index0, etc)
        /// NOTE:  This is readonly - any changes to this array won't be reflected by this class
        /// </summary>
        public int[] IndexArray
        {
            get
            {
                lock (_lock)
                {
                    if (_indexArray == null)
                        _indexArray = [Index0, Index1, Index2];

                    return _indexArray;
                }
            }
        }

        public int GetIndex(int whichIndex)
        {
            switch (whichIndex)
            {
                case 0:
                    return Index0;

                case 1:
                    return Index1;

                case 2:
                    return Index2;

                default:
                    throw new ArgumentOutOfRangeException("whichIndex", $"whichIndex can only be 0, 1, 2: {whichIndex}");
            }
        }
        public int GetIndex(TriangleEdge edge, bool isFrom)
        {
            switch (edge)
            {
                case TriangleEdge.Edge_01:
                    return isFrom ? Index0 : Index1;

                case TriangleEdge.Edge_12:
                    return isFrom ? Index1 : Index2;

                case TriangleEdge.Edge_20:
                    return isFrom ? Index2 : Index0;

                default:
                    throw new ApplicationException($"Unknown TriangleEdge: {edge}");
            }
        }
        public int GetCommonIndex(TriangleEdge edge0, TriangleEdge edge1)
        {
            int[] indices0 = [GetIndex(edge0, true), GetIndex(edge0, false)];
            int[] indices1 = [GetIndex(edge1, true), GetIndex(edge1, false)];

            for (int cntr0 = 0; cntr0 < indices0.Length; cntr0++)
                for (int cntr1 = 0; cntr1 < indices1.Length; cntr1++)
                    if (indices0[cntr0] == indices1[cntr1])
                        return indices0[cntr0];

            throw new ApplicationException("Didn't find a common index");
        }
        public int GetUncommonIndex(TriangleEdge edge0, TriangleEdge edge1)
        {
            int[] indices0 = [GetIndex(edge0, true), GetIndex(edge0, false)];
            int[] indices1 = [GetIndex(edge1, true), GetIndex(edge1, false)];

            for (int cntr0 = 0; cntr0 < indices0.Length; cntr0++)
                for (int cntr1 = 0; cntr1 < indices1.Length; cntr1++)
                    if (indices0[cntr0] == indices1[cntr1])
                        return indices0[cntr0 == 0 ? 1 : 0];        // return the one that isn't common

            throw new ApplicationException("Didn't find a common index");
        }
        public int GetOppositeIndex(TriangleEdge edge)
        {
            switch (edge)
            {
                case TriangleEdge.Edge_01:
                    return Index2;

                case TriangleEdge.Edge_12:
                    return Index0;

                case TriangleEdge.Edge_20:
                    return Index1;

                default:
                    throw new ApplicationException($"Unknown TriangleEdge: {edge}");
            }
        }

        #endregion
        #region IComparable<ITriangle> Members

        /// <summary>
        /// I wanted to be able to use triangles as keys in a sorted list
        /// </summary>
        public int CompareTo(ITriangle_wpf other)
        {
            if (other == null)
                return 1;       // greater than null

            if (Token < other.Token)
                return -1;

            else if (Token > other.Token)
                return 1;

            else
                return 0;
        }

        #endregion

        #region Public Methods

        public Triangle_wpf ToTriangle()
        {
            return new Triangle_wpf(Point0, Point1, Point2);
        }

        /// <summary>
        /// This helps a lot when looking at lists of triangles in the quick watch
        /// </summary>
        public override string ToString()
        {
            return string.Format("{0} - {1} - {2}       |       ({3}) ({4}) ({5})",
                _index0.ToString(),
                _index1.ToString(),
                _index2.ToString(),

                Point0.ToString(2),
                Point1.ToString(2),
                Point2.ToString(2));
        }

        /// <summary>
        /// This creates a clone of the triangles passed in, but the new list will only have points that are used
        /// </summary>
        public static ITriangleIndexed_wpf[] Clone_CondensePoints(ITriangleIndexed_wpf[] triangles)
        {
            // Analize the points
            GetCondensedPointMap(out Point3D[] allUsedPoints, out SortedList<int, int> oldToNewIndex, triangles);

            // Make new triangles that only have the used points
            TriangleIndexed_wpf[] retVal = new TriangleIndexed_wpf[triangles.Length];

            for (int cntr = 0; cntr < triangles.Length; cntr++)
                retVal[cntr] = new TriangleIndexed_wpf(oldToNewIndex[triangles[cntr].Index0], oldToNewIndex[triangles[cntr].Index1], oldToNewIndex[triangles[cntr].Index2], allUsedPoints);

            return retVal;
        }

        /// <summary>
        /// This clones the set of triangles, but with the points run through a transform
        /// </summary>
        public static ITriangleIndexed_wpf[] Clone_Transformed(ITriangleIndexed_wpf[] triangles, Transform3D transform)
        {
            if (triangles == null)
                return new TriangleIndexed_wpf[0];

            Point3D[] transformedPoints = triangles[0].AllPoints.
                Select(o => transform.Transform(o)).
                ToArray();

            return triangles.
                Select(o => new TriangleIndexed_wpf(o.Index0, o.Index1, o.Index2, transformedPoints)).
                ToArray();
        }

        /// <summary>
        /// This looks at all the lines in the triangles passed in, and returns the unique indices
        /// </summary>
        public static (int, int)[] GetUniqueLines(IEnumerable<ITriangleIndexed_wpf> triangles)
        {
            var retVal = new List<(int, int)>();

            foreach (ITriangleIndexed_wpf triangle in triangles)
            {
                retVal.Add(GetLine(triangle.Index0, triangle.Index1));
                retVal.Add(GetLine(triangle.Index1, triangle.Index2));
                retVal.Add(GetLine(triangle.Index2, triangle.Index0));
            }

            return retVal.
                Distinct().
                ToArray();		//distinct works, because the tuple always has the smaller index as item1
        }

        /// <summary>
        /// This returns only the points that are used across the triangles
        /// </summary>
        /// <param name="forcePointCompare">If the points should be directly compared (ignore indices), then pass true</param>
        public static Point3D[] GetUsedPoints(IEnumerable<ITriangleIndexed_wpf> triangles, bool forcePointCompare = false)
        {
            if (forcePointCompare)
                return Triangle_wpf.GetUniquePoints(triangles);

            ITriangleIndexed_wpf first = triangles.FirstOrDefault();
            if (first == null)
                return [];

            Point3D[] allPoints = first.AllPoints;

            // Since these are indexed triangles, dedupe on the indices.  This will be much faster than directly comparing points
            return triangles.
                SelectMany(o => o.IndexArray).
                Distinct().
                //OrderBy(o => o).
                Select(o => allPoints[o]).
                ToArray();
        }
        /// <summary>
        /// This overload takes in a bunch of sets of triangles.  Each set is independent of the others (unique AllPoints per set)
        /// </summary>
        public static Point3D[] GetUsedPoints(IEnumerable<IEnumerable<ITriangleIndexed_wpf>> triangleSets, bool forcePointCompare = false)
        {
            var retVal = new List<Point3D>();

            var pointCompare = new Func<Point3D, Point3D, bool>((p1, p2) => Math3D.IsNearValue(p1, p2));

            foreach (var set in triangleSets)
                retVal.AddRangeUnique(TriangleIndexed_wpf.GetUsedPoints(set, forcePointCompare), pointCompare);

            return retVal.ToArray();
        }

        public static TriangleIndexed_wpf[] ConvertToIndexed(ITriangle_wpf[] triangles)
        {
            // Find the unique points
            GetPointsAndIndices(out Point3D[] points, out Tuple<int, int, int>[] map, triangles);

            var retVal = new TriangleIndexed_wpf[triangles.Length];

            // Turn the map into triangles
            for (int cntr = 0; cntr < triangles.Length; cntr++)
                retVal[cntr] = new TriangleIndexed_wpf(map[cntr].Item1, map[cntr].Item2, map[cntr].Item3, points);

            return retVal;
        }

        /// <summary>
        /// Call this if the contents of AllPoints changed.  It will wipe out any cached variables
        /// </summary>
        public void PointsChanged()
        {
            lock (_lock)
                OnPointChanged();
        }

        #endregion
        #region Internal Methods

        /// <summary>
        /// This takes in triangles, and builds finds the unique points so that an array of TriangleIndexed
        /// can be built
        /// </summary>
        internal static void GetPointsAndIndices(out Point3D[] points, out Tuple<int, int, int>[] map, ITriangle_wpf[] triangles)
        {
            List<Point3D> pointList = new List<Point3D>();
            map = new Tuple<int, int, int>[triangles.Length];

            for (int cntr = 0; cntr < triangles.Length; cntr++)
            {
                map[cntr] = Tuple.Create(
                    FindOrAddPoint(pointList, triangles[cntr].Point0),
                    FindOrAddPoint(pointList, triangles[cntr].Point1),
                    FindOrAddPoint(pointList, triangles[cntr].Point2));
            }

            points = pointList.ToArray();
        }
        internal static int FindOrAddPoint(List<Point3D> points, Point3D newPoint)
        {
            // Try exact
            for (int cntr = 0; cntr < points.Count; cntr++)
                if (points[cntr] == newPoint)
                    return cntr;

            // Try close
            for (int cntr = 0; cntr < points.Count; cntr++)
                if (Math3D.IsNearValue(points[cntr], newPoint))
                    return cntr;

            // It's unique, add it
            points.Add(newPoint);
            return points.Count - 1;
        }

        #endregion
        #region Protected Methods

        //NOTE: This doesn't lock itself, it expects the caller to have taken a lock
        protected virtual void OnPointChanged()
        {
            _normal = null;
            _normalUnit = null;
            _normalLength = null;
            //_indexArray = null;       // moving points doesn't affect this
            _planeDistance = null;
        }

        /// <summary>
        /// This figures out which points in the list of triangles are used, and returns out to map from AllPoints to allUsedPoints
        /// </summary>
        /// <param name="allUsedPoints">These are only the points that are used</param>
        /// <param name="oldToNewIndex">
        /// Key = Index to a point from the list of triangles passed in.
        /// Value = Corresponding index into allUsedPoints.
        /// </param>
        protected static void GetCondensedPointMap(out Point3D[] allUsedPoints, out SortedList<int, int> oldToNewIndex, ITriangleIndexed_wpf[] triangles)
        {
            if (triangles == null || triangles.Length == 0)
            {
                allUsedPoints = new Point3D[0];
                oldToNewIndex = new SortedList<int, int>();
                return;
            }

            // Get all the used indices
            int[] allUsedIndices = triangles.
                SelectMany(o => o.IndexArray).
                Distinct().
                OrderBy(o => o).
                ToArray();

            // Get the points
            Point3D[] allPoints = triangles[0].AllPoints;
            allUsedPoints = allUsedIndices.
                Select(o => allPoints[o]).
                ToArray();

            // Build the map
            oldToNewIndex = new SortedList<int, int>();
            for (int cntr = 0; cntr < allUsedIndices.Length; cntr++)
                oldToNewIndex.Add(allUsedIndices[cntr], cntr);
        }

        #endregion

        #region Private Methods

        private static (int, int) GetLine(int index1, int index2)
        {
            return (Math.Min(index1, index2), Math.Max(index1, index2));
        }

        #endregion
    }

    #endregion
    #region class: TriangleIndexedLinked_wpf

    /// <summary>
    /// This one also knows about its neighbors
    /// </summary>
    public class TriangleIndexedLinked_wpf : TriangleIndexed_wpf, IComparable<TriangleIndexedLinked_wpf>
    {
        #region Declaration Section

        private readonly object _lock = new object();

        #endregion

        #region Constructor

        public TriangleIndexedLinked_wpf(int index0, int index1, int index2, Point3D[] allPoints)
            : base(index0, index1, index2, allPoints) { }

        #endregion

        #region IComparable<TriangleLinkedIndexed> Members

        // SortedList fails if it doesn't have an explicit CompareTo for this type
        public int CompareTo(TriangleIndexedLinked_wpf other)
        {
            // The compare is by token, so just call the base
            return base.CompareTo(other);
        }

        #endregion

        #region Public Properties

        // These neighbors share one vertex
        private TriangleIndexedLinked_CornerNeighbor[] _neighbor_0 = null;
        /// <summary>
        /// These are the other triangles that share the same point with Index0
        /// </summary>
        public TriangleIndexedLinked_CornerNeighbor[] Neighbor_0
        {
            get
            {
                lock (_lock)
                    return _neighbor_0;
            }
            set
            {
                lock (_lock)
                    _neighbor_0 = value;
            }
        }

        private TriangleIndexedLinked_CornerNeighbor[] _neighbor_1 = null;
        /// <summary>
        /// These are the other triangles that share the same point with Index1
        /// </summary>
        public TriangleIndexedLinked_CornerNeighbor[] Neighbor_1
        {
            get
            {
                lock (_lock)
                    return _neighbor_1;
            }
            set
            {
                lock (_lock)
                    _neighbor_1 = value;
            }
        }

        private TriangleIndexedLinked_CornerNeighbor[] _neighbor_2 = null;
        /// <summary>
        /// These are the other triangles that share the same point with Index2
        /// </summary>
        public TriangleIndexedLinked_CornerNeighbor[] Neighbor_2
        {
            get
            {
                lock (_lock)
                    return _neighbor_2;
            }
            set
            {
                lock (_lock)
                    _neighbor_2 = value;
            }
        }

        // These neighbors share two vertices
        //TODO: It's possible for more than two triangles to share an edge
        private TriangleIndexedLinked_wpf _neighbor_01 = null;
        public TriangleIndexedLinked_wpf Neighbor_01
        {
            get
            {
                lock (_lock)
                    return _neighbor_01;
            }
            set
            {
                lock (_lock)
                    _neighbor_01 = value;
            }
        }

        private TriangleIndexedLinked_wpf _neighbor_12 = null;
        public TriangleIndexedLinked_wpf Neighbor_12
        {
            get
            {
                lock (_lock)
                    return _neighbor_12;
            }
            set
            {
                lock (_lock)
                    _neighbor_12 = value;
            }
        }

        private TriangleIndexedLinked_wpf _neighbor_20 = null;
        public TriangleIndexedLinked_wpf Neighbor_20
        {
            get
            {
                lock (_lock)
                    return _neighbor_20;
            }
            set
            {
                lock (_lock)
                    _neighbor_20 = value;
            }
        }

        private int[] _indexArraySorted = null;
        /// <summary>
        /// This returns a sorted array (useful for comparing triangles)
        /// NOTE: This is readonly - any changes to this array won't be reflected by this class
        /// </summary>
        public int[] IndexArraySorted
        {
            get
            {
                lock (_lock)
                {
                    if (_indexArraySorted == null)
                    {
                        _indexArraySorted = new int[] { Index0, Index1, Index2 }.
                            OrderBy(o => o).
                            ToArray();
                    }

                    return _indexArraySorted;
                }
            }
        }

        #endregion

        #region Public Methods

        // These let you get at the neighbors with an enum instead of calling the explicit properties
        public TriangleIndexedLinked_wpf GetNeighbor(TriangleEdge edge)
        {
            switch (edge)
            {
                case TriangleEdge.Edge_01:
                    return Neighbor_01;

                case TriangleEdge.Edge_12:
                    return Neighbor_12;

                case TriangleEdge.Edge_20:
                    return Neighbor_20;

                default:
                    throw new ApplicationException("Unknown TriangleEdge: " + edge.ToString());
            }
        }
        public void SetNeighbor(TriangleEdge edge, TriangleIndexedLinked_wpf neighbor)
        {
            switch (edge)
            {
                case TriangleEdge.Edge_01:
                    Neighbor_01 = neighbor;
                    break;

                case TriangleEdge.Edge_12:
                    Neighbor_12 = neighbor;
                    break;

                case TriangleEdge.Edge_20:
                    Neighbor_20 = neighbor;
                    break;

                default:
                    throw new ApplicationException("Unknown TriangleEdge: " + edge.ToString());
            }
        }
        public TriangleIndexedLinked_CornerNeighbor[] GetNeighbors(TriangleCorner corner)
        {
            switch (corner)
            {
                case TriangleCorner.Corner_0:
                    return Neighbor_0;

                case TriangleCorner.Corner_1:
                    return Neighbor_1;

                case TriangleCorner.Corner_2:
                    return Neighbor_2;

                default:
                    throw new ApplicationException("Unknown TriangleCorner: " + corner.ToString());
            }
        }
        public TriangleIndexedLinked_CornerNeighbor[] GetNeighbors(int cornerIndex)
        {
            if (cornerIndex == Index0)
                return Neighbor_0;

            else if (cornerIndex == Index1)
                return Neighbor_1;

            else if (cornerIndex == Index2)
                return Neighbor_2;

            else
                throw new ApplicationException($"Didn't find index: {cornerIndex} in {Index0},{Index1},{Index2}");
        }

        //NOTE: These return how the neighbor is related when walking from this to the neighbor
        public TriangleEdge WhichEdge(TriangleIndexedLinked_wpf neighbor)
        {
            long neighborToken = neighbor.Token;

            if (Neighbor_01 != null && Neighbor_01.Token == neighborToken)
                return TriangleEdge.Edge_01;

            else if (Neighbor_12 != null && Neighbor_12.Token == neighborToken)
                return TriangleEdge.Edge_12;

            else if (Neighbor_20 != null && Neighbor_20.Token == neighborToken)
                return TriangleEdge.Edge_20;

            else
                throw new ApplicationException($"Not a neighbor\r\n{this}\r\n{neighbor}");
        }
        public TriangleCorner WhichCorner(TriangleIndexedLinked_wpf neighbor)
        {
            long neighborToken = neighbor.Token;

            if (Neighbor_0 != null && Neighbor_0.Any(o => o.Triangle.Token == neighborToken))
                return TriangleCorner.Corner_0;

            else if (Neighbor_1 != null && Neighbor_1.Any(o => o.Triangle.Token == neighborToken))
                return TriangleCorner.Corner_1;

            else if (Neighbor_2 != null && Neighbor_2.Any(o => o.Triangle.Token == neighborToken))
                return TriangleCorner.Corner_2;

            else
                throw new ApplicationException($"Not a neighbor\r\n{this}\r\n{neighbor}");
        }

        public static List<TriangleIndexedLinked_wpf> ConvertToLinked(List<ITriangleIndexed_wpf> triangles, bool linkEdges, bool linkCorners)
        {
            var retVal = triangles.
                Select(o => new TriangleIndexedLinked_wpf(o.Index0, o.Index1, o.Index2, o.AllPoints)).
                ToList();

            if (linkEdges)
                LinkTriangles_Edges(retVal, true);

            if (linkCorners)
                LinkTriangles_Corners(retVal, true);

            return retVal;
        }
        public static TriangleIndexedLinked_wpf[] ConvertToLinked(ITriangleIndexed_wpf[] triangles, bool linkEdges, bool linkCorners)
        {
            var retVal = triangles.
                Select(o => new TriangleIndexedLinked_wpf(o.Index0, o.Index1, o.Index2, o.AllPoints)).
                ToList();

            if (linkEdges)
                LinkTriangles_Edges(retVal, true);

            if (linkCorners)
                LinkTriangles_Corners(retVal, true);

            return retVal.ToArray();
        }

        /// <summary>
        /// This does a brute force scan of the triangles, and finds the adjacent triangles (doesn't look at shared corners, only edges)
        /// </summary>
        /// <param name="setNullIfNoLink">
        /// True:  If no link is found for an edge, that edge is set to null
        /// False:  If no link is found for an edge, that edge is left alone
        /// </param>
        public static void LinkTriangles_Edges(List<TriangleIndexedLinked_wpf> triangles, bool setNullIfNoLink)
        {
            triangles.
                AsParallel().
                ForAll(o =>
                {
                    TriangleIndexedLinked_wpf neighbor = FindLinkEdge(triangles, o.Token, o.Index0, o.Index1);
                    if (neighbor != null || setNullIfNoLink)
                        o.Neighbor_01 = neighbor;

                    neighbor = FindLinkEdge(triangles, o.Token, o.Index1, o.Index2);
                    if (neighbor != null || setNullIfNoLink)
                        o.Neighbor_12 = neighbor;

                    neighbor = FindLinkEdge(triangles, o.Token, o.Index2, o.Index0);
                    if (neighbor != null || setNullIfNoLink)
                        o.Neighbor_20 = neighbor;
                });
        }

        /// <summary>
        /// This does a brute force scan of the triangles, and finds the adjacent triangles (doesn't look at shared edges, only corners)
        /// </summary>
        /// <param name="setNullIfNoLink">
        /// True:  If no link is found for an edge, that edge is set to null
        /// False:  If no link is found for an edge, that edge is left alone
        /// </param>
        public static void LinkTriangles_Corners(List<TriangleIndexedLinked_wpf> triangles, bool setNullIfNoLink)
        {

            // TODO: this function looks like it could be optimized - at least add some parallel processing.  If corners are actually needed, then optimize it


            if (setNullIfNoLink)
            {
                foreach (TriangleIndexedLinked_wpf triangle in triangles)
                {
                    triangle.Neighbor_0 = null;
                    triangle.Neighbor_1 = null;
                    triangle.Neighbor_2 = null;
                }
            }

            if (triangles.Count <= 1)
                return;

            //NOTE: This method only works if all triangles use the same points list
            foreach (int index in Enumerable.Range(0, triangles[0].AllPoints.Length))
            {
                var used = new List<TriangleIndexedLinked_CornerNeighbor>();

                // Find the triangles with this index
                foreach (TriangleIndexedLinked_wpf triangle in triangles)
                {
                    if (triangle.Index0 == index)
                        used.Add(new TriangleIndexedLinked_CornerNeighbor()
                        {
                            Triangle = triangle,
                            CornerIndex = 0,
                            WhichCorner = TriangleCorner.Corner_0,
                        });

                    else if (triangle.Index1 == index)
                        used.Add(new TriangleIndexedLinked_CornerNeighbor()
                        {
                            Triangle = triangle,
                            CornerIndex = 1,
                            WhichCorner = TriangleCorner.Corner_1,
                        });

                    else if (triangle.Index2 == index)
                        used.Add(new TriangleIndexedLinked_CornerNeighbor()
                        {
                            Triangle = triangle,
                            CornerIndex = 2,
                            WhichCorner = TriangleCorner.Corner_2,
                        });
                }

                if (used.Count <= 1)
                    continue;

                // Distribute them
                for (int cntr = 0; cntr < used.Count; cntr++)
                {
                    var neighbors = used.
                        Where((o, i) => i != cntr).
                        ToArray();

                    switch (used[cntr].WhichCorner)
                    {
                        case TriangleCorner.Corner_0:
                            used[cntr].Triangle.Neighbor_0 = neighbors;
                            break;

                        case TriangleCorner.Corner_1:
                            used[cntr].Triangle.Neighbor_1 = neighbors;
                            break;

                        case TriangleCorner.Corner_2:
                            used[cntr].Triangle.Neighbor_2 = neighbors;
                            break;

                        default:
                            throw new ApplicationException("Unknown TriangleCorner: " + used[cntr].WhichCorner.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// This does a brute force scan of the triangles, and finds the adjacent triangles (both shared edges and corners)
        /// </summary>
        /// <param name="setNullIfNoLink">
        /// True:  If no link is found for an edge, that edge is set to null
        /// False:  If no link is found for an edge, that edge is left alone
        /// </param>
        public static void LinkTriangles_Both(List<TriangleIndexedLinked_wpf> triangles, bool setNullIfNoLink)
        {
            LinkTriangles_Corners(triangles, setNullIfNoLink);
            LinkTriangles_Edges(triangles, setNullIfNoLink);
        }

        /// <summary>
        /// Only call this method if you know the two triangles are neighbors 
        /// </summary>
        public static void LinkTriangles_Edges(TriangleIndexedLinked_wpf triangle1, TriangleIndexedLinked_wpf triangle2)
        {
            foreach (TriangleEdge edge1 in Triangle_wpf.Edges)
            {
                int[] indices1 = new int[2];
                triangle1.GetIndices(out indices1[0], out indices1[1], edge1);

                foreach (TriangleEdge edge2 in Triangle_wpf.Edges)
                {
                    int[] indices2 = new int[2];
                    triangle2.GetIndices(out indices2[0], out indices2[1], edge2);

                    if ((indices1[0] == indices2[0] && indices1[1] == indices2[1]) ||
                        (indices1[0] == indices2[1] && indices1[1] == indices2[0]))
                    {
                        triangle1.SetNeighbor(edge1, triangle2);
                        triangle2.SetNeighbor(edge2, triangle1);
                        return;
                    }
                }
            }

            throw new ArgumentException(string.Format("The two triangles passed in don't share an edge:\r\n{0}\r\n{1}", triangle1.ToString(), triangle2.ToString()));
        }

        public int GetIndex(TriangleCorner corner)
        {
            switch (corner)
            {
                case TriangleCorner.Corner_0:
                    return Index0;

                case TriangleCorner.Corner_1:
                    return Index1;

                case TriangleCorner.Corner_2:
                    return Index2;

                default:
                    throw new ApplicationException($"Unknown TriangleCorner: {corner}");
            }
        }
        public void GetIndices(out int index1, out int index2, TriangleEdge edge)
        {
            switch (edge)
            {
                case TriangleEdge.Edge_01:
                    index1 = Index0;
                    index2 = Index1;
                    break;

                case TriangleEdge.Edge_12:
                    index1 = Index1;
                    index2 = Index2;
                    break;

                case TriangleEdge.Edge_20:
                    index1 = Index2;
                    index2 = Index0;
                    break;

                default:
                    throw new ApplicationException($"Unknown TriangleEdge: {edge}");
            }
        }

        public Point3D GetPoint(TriangleCorner corner)
        {
            switch (corner)
            {
                case TriangleCorner.Corner_0:
                    return Point0;

                case TriangleCorner.Corner_1:
                    return Point1;

                case TriangleCorner.Corner_2:
                    return Point2;

                default:
                    throw new ApplicationException($"Unknown TriangleCorner: {corner}");
            }
        }
        public void GetPoints(out Point3D point1, out Point3D point2, TriangleEdge edge)
        {
            switch (edge)
            {
                case TriangleEdge.Edge_01:
                    point1 = Point0;
                    point2 = Point1;
                    break;

                case TriangleEdge.Edge_12:
                    point1 = Point1;
                    point2 = Point2;
                    break;

                case TriangleEdge.Edge_20:
                    point1 = Point2;
                    point2 = Point0;
                    break;

                default:
                    throw new ApplicationException($"Unknown TriangleEdge: {edge}");
            }
        }

        /// <summary>
        /// Pass in two corners, and this returns what edge they are
        /// </summary>
        public static TriangleEdge GetEdge(TriangleCorner corner0, TriangleCorner corner1)
        {
            switch (corner0)
            {
                case TriangleCorner.Corner_0:
                    switch (corner1)
                    {
                        case TriangleCorner.Corner_1:
                            return TriangleEdge.Edge_01;

                        case TriangleCorner.Corner_2:
                            return TriangleEdge.Edge_20;

                        default:
                            throw new ApplicationException("Unexpected TriangleCorner: " + corner0);
                    }

                case TriangleCorner.Corner_1:
                    switch (corner1)
                    {
                        case TriangleCorner.Corner_0:
                            return TriangleEdge.Edge_01;

                        case TriangleCorner.Corner_2:
                            return TriangleEdge.Edge_12;

                        default:
                            throw new ApplicationException("Unexpected TriangleCorner: " + corner0);
                    }

                case TriangleCorner.Corner_2:
                    switch (corner1)
                    {
                        case TriangleCorner.Corner_0:
                            return TriangleEdge.Edge_20;

                        case TriangleCorner.Corner_1:
                            return TriangleEdge.Edge_12;

                        default:
                            throw new ApplicationException("Unexpected TriangleCorner: " + corner0);
                    }

                default:
                    throw new ApplicationException("Unknown TriangleCorner: " + corner0);
            }
        }

        public bool IsMatch(int[] indeciesSorted)
        {
            int[] mySorted = IndexArraySorted;

            // Speed is important, so I'm skipping range checking, and looping

            return mySorted[0] == indeciesSorted[0] &&
                mySorted[1] == indeciesSorted[1] &&
                mySorted[2] == indeciesSorted[2];
        }

        #endregion

        #region Private Methods

        private static TriangleIndexedLinked_wpf FindLinkEdge(List<TriangleIndexedLinked_wpf> triangles, long calling_token, int vertex1, int vertex2)
        {
            // Find the triangle that has vertex1 and 2
            for (int i = 0; i < triangles.Count; i++)
            {
                if (triangles[i].Token == calling_token)
                    // This is the currently requested triangle, so ignore it
                    continue;

                if ((triangles[i].Index0 == vertex1 && triangles[i].Index1 == vertex2) ||
                    (triangles[i].Index0 == vertex1 && triangles[i].Index2 == vertex2) ||
                    (triangles[i].Index1 == vertex1 && triangles[i].Index0 == vertex2) ||
                    (triangles[i].Index1 == vertex1 && triangles[i].Index2 == vertex2) ||
                    (triangles[i].Index2 == vertex1 && triangles[i].Index0 == vertex2) ||
                    (triangles[i].Index2 == vertex1 && triangles[i].Index1 == vertex2))
                {
                    // Found it
                    //NOTE:  Just returning the first match.  Assuming that the list of triangles is a valid hull
                    //NOTE:  Not validating that the triangle has 3 unique points, and the 2 points passed in are unique
                    return triangles[i];
                }
            }

            // No neighbor found
            return null;
        }
        private static TriangleIndexedLinked_wpf FindLinkCorner(List<TriangleIndexedLinked_wpf> triangles, long calling_token, int cornerVertex, int otherVertex1, int otherVertex2)
        {
            // Find the triangle that has cornerVertex, but not otherVertex1 or 2
            for (int i = 0; i < triangles.Count; i++)
            {
                if (triangles[i].Token == calling_token)
                    // This is the currently requested triangle, so ignore it
                    continue;

                if ((triangles[i].Index0 == cornerVertex && triangles[i].Index1 != otherVertex1 && triangles[i].Index2 != otherVertex1 && triangles[i].Index1 != otherVertex2 && triangles[i].Index2 != otherVertex2) ||
                    (triangles[i].Index1 == cornerVertex && triangles[i].Index0 != otherVertex1 && triangles[i].Index2 != otherVertex1 && triangles[i].Index0 != otherVertex2 && triangles[i].Index2 != otherVertex2) ||
                    (triangles[i].Index2 == cornerVertex && triangles[i].Index0 != otherVertex1 && triangles[i].Index1 != otherVertex1 && triangles[i].Index0 != otherVertex2 && triangles[i].Index1 != otherVertex2))
                {
                    // Found it
                    //NOTE:  Just returning the first match.  Assuming that the list of triangles is a valid hull
                    //NOTE:  Not validating that the triangle has 3 unique points, and the 3 points passed in are unique
                    return triangles[i];
                }
            }

            // No neighbor found
            return null;
        }

        #endregion
    }

    #endregion
    #region record: TriangleIndexedLinked_CornerNeighbor

    public record TriangleIndexedLinked_CornerNeighbor
    {
        public TriangleIndexedLinked_wpf Triangle { get; init; }
        public int CornerIndex { get; init; }
        public TriangleCorner WhichCorner { get; init; }
    }

    #endregion

    #region interface: ITriangle_wpf

    //TODO:  May want more readonly statistics methods, like IsIntersecting, is Acute/Right/Obtuse
    public interface ITriangle_wpf : IComparable<ITriangle_wpf>
    {
        Point3D Point0 { get; }
        Point3D Point1 { get; }
        Point3D Point2 { get; }

        Point3D[] PointArray { get; }

        Point3D this[int index] { get; }

        Vector3D Normal { get; }
        /// <summary>
        /// This returns the triangle's normal.  Its length is one
        /// </summary>
        Vector3D NormalUnit { get; }
        /// <summary>
        /// This returns the length of the normal (the area of the triangle)
        /// NOTE:  Call this if you just want to know the length of the normal, it's cheaper than calling Normal.Length, since it's already been calculated
        /// </summary>
        double NormalLength { get; }

        /// <summary>
        /// This is useful for functions that use this triangle as the definition of a plane
        /// (normal * planeDist = 0)
        /// </summary>
        double PlaneDistance { get; }

        Point3D GetCenterPoint();
        Point3D GetPoint(TriangleEdge edge, bool isFrom);
        Point3D GetCommonPoint(TriangleEdge edge0, TriangleEdge edge1);
        /// <summary>
        /// Returns the point in edge0 that isn't in edge1
        /// </summary>
        Point3D GetUncommonPoint(TriangleEdge edge0, TriangleEdge edge1);
        Point3D GetOppositePoint(TriangleEdge edge);
        Point3D GetEdgeMidpoint(TriangleEdge edge);
        double GetEdgeLength(TriangleEdge edge);

        long Token { get; }
    }

    #endregion
    #region interface: ITriangleIndexed_wpf

    public interface ITriangleIndexed_wpf : ITriangle_wpf
    {
        int Index0 { get; }
        int Index1 { get; }
        int Index2 { get; }

        Point3D[] AllPoints { get; }

        int[] IndexArray { get; }

        int GetIndex(int whichIndex);
        int GetIndex(TriangleEdge edge, bool isFrom);
        int GetCommonIndex(TriangleEdge edge0, TriangleEdge edge1);
        int GetUncommonIndex(TriangleEdge edge0, TriangleEdge edge1);
        int GetOppositeIndex(TriangleEdge edge);
    }

    #endregion
}
