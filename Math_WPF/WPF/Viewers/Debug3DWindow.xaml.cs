﻿using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF.Controls3D;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Xaml;

namespace Game.Math_WPF.WPF.Viewers
{
    //TODO: Add a method Show_OtherProcess().  This will let them visualize a function they are debugging without stepping out of the function

    /// <summary>
    /// This is meant to be used when diagnosing an error.  Make a copy of your buggy function, instantiate a debug window
    /// from within the copy, draw the scene to try to help visualize the problem
    /// </summary>
    /// <remarks>
    /// All of the drawing functions were split into Add and a static Get.  The gets are meant to be able to be used elsewhere if
    /// you need to draw a scene in some other window
    /// 
    /// Note that these methods may not be optimal, the priority is having simple to use functions.  Having lots of Visual3D
    /// instances is really bad for performance.  It's better to have a model group with a single visual
    /// </remarks>
    public partial class Debug3DWindow : Window
    {
        #region class: GraphMouseTracker

        private class GraphMouseTracker
        {
            public GraphResult Graph { get; set; }
            public Rect3D Location { get; set; }
            public ModelVisual3D Visual { get; set; }
            public Action<GraphMouseArgs> Delegate { get; set; }
        }

        #endregion
        #region record: ArrowProps

        private record ArrowProps
        {
            public Point3D Point1_Adjusted { get; init; }
            public Point3D Point2_Adjusted { get; init; }
            public double Radius { get; init; }
            public double Height { get; init; }
            public Vector3D Direction_Unit { get; init; }
        }

        #endregion

        #region Declaration Section

        public const string AXISCOLOR_X = "FF6060";
        public const string AXISCOLOR_Y = "00C000";
        public const string AXISCOLOR_Z = "6060FF";

        private TrackBallRoam _trackball = null;

        private bool _wasSetCameraCalled = false;
        private bool _loaded = false;

        private readonly int _viewportOffset;

        private readonly List<GraphMouseTracker> _graphMouseTrackers = new List<GraphMouseTracker>();

        private readonly List<ScreenSpaceLines3D> _screenspaceLines = new List<ScreenSpaceLines3D>();

        #endregion

        #region Constructor

        public Debug3DWindow()
        {
            Messages_Top = new ObservableCollection<UIElement>();       // these appear to need to be created before initializecomponent
            Messages_Bottom = new ObservableCollection<UIElement>();

            Visuals3D = new ObservableCollection<Visual3D>();
            Visuals3D.CollectionChanged += Visuals3D_CollectionChanged;        // can't bind viewport directly to this, because the camera and lights need to be left alone

            InitializeComponent();

            DataContext = this;

            _viewportOffset = _viewport.Children.Count;
        }

        #endregion

        #region Public Properties

        //public ObservableCollection<Visual3D> Visuals3D { get; private set; }
        public ObservableCollection<Visual3D> Visuals3D { get; private set; }

        // These can be used to show debug messages at the top or bottom of the window
        public ObservableCollection<UIElement> Messages_Top { get; private set; }       // I think these MUST be properties for binding to see them (not public variables)
        public ObservableCollection<UIElement> Messages_Bottom { get; private set; }

        // These can be used to change the color of the lights
        public Color LightColor_Ambient
        {
            get { return (Color)GetValue(LightColor_AmbientProperty); }
            set { SetValue(LightColor_AmbientProperty, value); }
        }
        public static readonly DependencyProperty LightColor_AmbientProperty = DependencyProperty.Register("LightColor_Ambient", typeof(Color), typeof(Debug3DWindow), new PropertyMetadata(Colors.DimGray));

        public Color LightColor_Primary
        {
            get { return (Color)GetValue(LightColor_PrimaryProperty); }
            set { SetValue(LightColor_PrimaryProperty, value); }
        }
        public static readonly DependencyProperty LightColor_PrimaryProperty = DependencyProperty.Register("LightColor_Primary", typeof(Color), typeof(Debug3DWindow), new PropertyMetadata(Colors.White));

        public Color LightColor_Secondary
        {
            get { return (Color)GetValue(LightColor_SecondaryProperty); }
            set { SetValue(LightColor_SecondaryProperty, value); }
        }
        public static readonly DependencyProperty LightColor_SecondaryProperty = DependencyProperty.Register("LightColor_Secondary", typeof(Color), typeof(Debug3DWindow), new PropertyMetadata(Colors.Silver));

        private double? _trackball_InertiaPercentRetainPerSecond_Linear = .03;
        public double? Trackball_InertiaPercentRetainPerSecond_Linear
        {
            get
            {
                return _trackball_InertiaPercentRetainPerSecond_Linear;
            }
            set
            {
                _trackball_InertiaPercentRetainPerSecond_Linear = value;

                if (_trackball != null)
                    _trackball.InertiaPercentRetainPerSecond_Linear = value;
            }
        }
        private double? _trackball_InertiaPercentRetainPerSecond_Angular = .03;
        public double? Trackball_InertiaPercentRetainPerSecond_Angular
        {
            get
            {
                return _trackball_InertiaPercentRetainPerSecond_Angular;
            }
            set
            {
                _trackball_InertiaPercentRetainPerSecond_Angular = value;

                if (_trackball != null)
                    _trackball.InertiaPercentRetainPerSecond_Angular = value;
            }
        }

        private bool _trackball_ShouldHitTestOnOrbit = false;
        public bool Trackball_ShouldHitTestOnOrbit
        {
            get
            {
                return _trackball_ShouldHitTestOnOrbit;
            }
            set
            {
                _trackball_ShouldHitTestOnOrbit = value;

                if (_trackball != null)
                    _trackball.ShouldHitTestOnOrbit = value;
            }
        }

        public Vector3D Camera_Look
        {
            get
            {
                if (_loaded)
                    return _camera.LookDirection.LengthSquared.IsNearValue(1) ?
                        _camera.LookDirection :
                        _camera.LookDirection.ToUnit();

                else
                    return new Vector3D(0, 0, -1);
            }
        }
        public Vector3D Camera_Up
        {
            get
            {
                if (_loaded)
                    return _camera.UpDirection.LengthSquared.IsNearValue(1) ?
                        _camera.UpDirection :
                        _camera.UpDirection.ToUnit();

                else
                    return new Vector3D(0, 1, 0);
            }
        }
        public Vector3D Camera_Right => Vector3D.CrossProduct(Camera_Look, Camera_Up);

        #endregion

        #region Public Methods

        public void Clear()
        {
            Visuals3D.Clear();
            Messages_Top.Clear();
            Messages_Bottom.Clear();
            panelSnapshots.Children.Clear();
        }

        public Visual3D[] AddAxisLines(double length, double thickness)
        {
            var retVal = GetAxisLines(length, thickness);

            Visuals3D.AddRange(retVal);

            return retVal;
        }
        public static Visual3D[] GetAxisLines(double length, double thickness)
        {
            return new[]
            {
                GetLine(new Point3D(0, 0, 0), new Point3D(length, 0, 0), thickness, UtilityWPF.ColorFromHex(AXISCOLOR_X)),
                GetLine(new Point3D(0, 0, 0), new Point3D(0, length, 0), thickness, UtilityWPF.ColorFromHex(AXISCOLOR_Y)),
                GetLine(new Point3D(0, 0, 0), new Point3D(0, 0, length), thickness, UtilityWPF.ColorFromHex(AXISCOLOR_Z)),
            };
        }

        public Visual3D AddDot(Point3D position, double radius, Color color, bool isShiny = true, bool isHiRes = false)
        {
            var retVal = GetDot(position, radius, color, isShiny, isHiRes);

            Visuals3D.Add(retVal);

            return retVal;
        }
        public Visual3D AddDot(Vector3D position, double radius, Color color, bool isShiny = true, bool isHiRes = false)
        {
            return AddDot(position.ToPoint(), radius, color, isShiny, isHiRes);
        }
        public static Visual3D GetDot(Point3D position, double radius, Color color, bool isShiny = true, bool isHiRes = false)
        {
            Material material = GetMaterial(isShiny, color);

            GeometryModel3D geometry = new GeometryModel3D();
            geometry.Material = material;
            geometry.BackMaterial = material;
            geometry.Geometry = UtilityWPF.GetSphere_Ico(radius, isHiRes ? 3 : 1, true);
            geometry.Transform = new TranslateTransform3D(position.ToVector());

            return new ModelVisual3D
            {
                Content = geometry
            };
        }

        public Visual3D AddDots(IEnumerable<Point3D> positions, double radius, Color color, bool isShiny = true, bool isHiRes = false)
        {
            var retVal = GetDots(positions, radius, color, isShiny, isHiRes);

            Visuals3D.Add(retVal);

            return retVal;
        }
        public Visual3D AddDots(IEnumerable<Vector3D> positions, double radius, Color color, bool isShiny = true, bool isHiRes = false)
        {
            return AddDots(positions.Select(o => o.ToPoint()), radius, color, isShiny, isHiRes);
        }
        public static Visual3D GetDots(IEnumerable<Point3D> positions, double radius, Color color, bool isShiny = true, bool isHiRes = false)
        {
            Model3DGroup geometries = new Model3DGroup();

            Material material = GetMaterial(isShiny, color);

            foreach (Point3D pos in positions)
            {
                GeometryModel3D geometry = new GeometryModel3D();
                geometry.Material = material;
                geometry.BackMaterial = material;
                geometry.Geometry = UtilityWPF.GetSphere_Ico(radius, isHiRes ? 3 : 1, true);
                geometry.Transform = new TranslateTransform3D(pos.ToVector());

                geometries.Children.Add(geometry);
            }

            return new ModelVisual3D
            {
                Content = geometries
            };
        }

        public Visual3D AddDots(IEnumerable<(Point3D position, double radius, Color color, bool isShiny, bool isHiRes)> definitions)
        {
            var retVal = GetDots(definitions);

            Visuals3D.Add(retVal);

            return retVal;
        }
        public static Visual3D GetDots(IEnumerable<(Point3D position, double radius, Color color, bool isShiny, bool isHiRes)> definitions)
        {
            Model3DGroup geometries = new Model3DGroup();

            foreach (var def in definitions)
            {
                Material material = GetMaterial(def.isShiny, def.color);

                geometries.Children.Add(new GeometryModel3D
                {
                    Material = material,
                    BackMaterial = material,
                    Geometry = UtilityWPF.GetSphere_Ico(def.radius, def.isHiRes ? 3 : 1, true),
                    Transform = new TranslateTransform3D(def.position.ToVector()),
                });
            }

            return new ModelVisual3D()
            {
                Content = geometries,
            };
        }

        public Visual3D AddEllipsoid(Point3D position, Vector3D radius, Color color, bool isShiny = true, bool isHiRes = false, Quaternion? rotation = null)
        {
            var retVal = GetEllipsoid(position, radius, color, isShiny, isHiRes, rotation);

            Visuals3D.Add(retVal);

            return retVal;
        }
        public static Visual3D GetEllipsoid(Point3D position, Vector3D radius, Color color, bool isShiny = true, bool isHiRes = false, Quaternion? rotation = null)
        {
            Material material = GetMaterial(isShiny, color);

            GeometryModel3D geometry = new GeometryModel3D();
            geometry.Material = material;
            geometry.BackMaterial = material;
            geometry.Geometry = UtilityWPF.GetSphere_Ico(1, isHiRes ? 3 : 1, true);

            Transform3DGroup transform = new Transform3DGroup();

            transform.Children.Add(new ScaleTransform3D(radius));

            if (rotation != null && rotation != Quaternion.Identity)
                transform.Children.Add(new RotateTransform3D(new QuaternionRotation3D(rotation.Value)));

            transform.Children.Add(new TranslateTransform3D(position.ToVector()));

            geometry.Transform = transform;

            return new ModelVisual3D
            {
                Content = geometry
            };
        }

        public Visual3D AddLine(Point3D point1, Point3D point2, double thickness, Color color, bool arrow_left = false, bool arrow_right = false)
        {
            var retVal = GetLine(point1, point2, thickness, color, arrow_left, arrow_right);

            Visuals3D.Add(retVal);

            return retVal;
        }
        public Visual3D AddLine(Vector3D point1, Vector3D point2, double thickness, Color color, bool arrow_left = false, bool arrow_right = false)
        {
            return AddLine(point1.ToPoint(), point2.ToPoint(), thickness, color, arrow_left, arrow_right);
        }
        public Visual3D AddLine(Point3D point1, Point3D point2, double thickness, Color colorFrom, Color colorTo, bool arrow_left = false, bool arrow_right = false)
        {
            var retVal = GetLine(point1, point2, thickness, colorFrom, colorTo, arrow_left, arrow_right);

            Visuals3D.Add(retVal);

            return retVal;
        }
        public Visual3D AddLine(Vector3D point1, Vector3D point2, double thickness, Color colorFrom, Color colorTo, bool arrow_left = false, bool arrow_right = false)
        {
            return AddLine(point1.ToPoint(), point2.ToPoint(), thickness, colorFrom, colorTo, arrow_left, arrow_right);
        }

        //TODO: left and right arrows are switched

        public static Visual3D GetLine(Point3D point1, Point3D point2, double thickness, Color color, bool arrow_left = false, bool arrow_right = false)
        {
            var arrow = arrow_left || arrow_right ?
                GetArrowProps(point1, point2, arrow_left, arrow_right, thickness) :
                new ArrowProps() { Point1_Adjusted = point1, Point2_Adjusted = point2 };

            Model3D model_line = new BillboardLine3D()
            {
                //Material = BillboardLine3D.GetLinearGradientMaterial_Unlit(colorFrom, colorTo),
                Color = color,
                FromPoint = arrow.Point1_Adjusted,
                ToPoint = arrow.Point2_Adjusted,
                Thickness = thickness,
            }.Model;

            if (!arrow_left && !arrow_right)
                return new ModelVisual3D()
                {
                    Content = model_line,
                };

            Model3DGroup model_group = new Model3DGroup();
            model_group.Children.Add(model_line);

            if (arrow_left)
                model_group.Children.Add(GetArrowTip(arrow.Point2_Adjusted, arrow.Point1_Adjusted, -arrow.Direction_Unit, arrow.Radius, arrow.Height, color));

            if (arrow_right)
                model_group.Children.Add(GetArrowTip(arrow.Point1_Adjusted, arrow.Point2_Adjusted, arrow.Direction_Unit, arrow.Radius, arrow.Height, color));

            return new ModelVisual3D()
            {
                Content = model_group,
            };
        }
        public static Visual3D GetLine(Point3D point1, Point3D point2, double thickness, Color colorFrom, Color colorTo, bool arrow_left = false, bool arrow_right = false)
        {
            var arrow = arrow_left || arrow_right ?
                GetArrowProps(point1, point2, arrow_left, arrow_right, thickness) :
                new ArrowProps() { Point1_Adjusted = point1, Point2_Adjusted = point2 };

            Model3D model_line = new BillboardLine3D()
            {
                //Material = BillboardLine3D.GetLinearGradientMaterial_Unlit(colorFrom, colorTo),
                ColorTo = colorTo,
                Color = colorFrom,
                FromPoint = arrow.Point1_Adjusted,
                ToPoint = arrow.Point2_Adjusted,
                Thickness = thickness,
            }.Model;

            if (!arrow_left && !arrow_right)
                return new ModelVisual3D()
                {
                    Content = model_line,
                };

            Model3DGroup model_group = new Model3DGroup();
            model_group.Children.Add(model_line);

            if (arrow_left)
                model_group.Children.Add(GetArrowTip(arrow.Point2_Adjusted, arrow.Point1_Adjusted, -arrow.Direction_Unit, arrow.Radius, arrow.Height, colorFrom));

            if (arrow_right)
                model_group.Children.Add(GetArrowTip(arrow.Point1_Adjusted, arrow.Point2_Adjusted, arrow.Direction_Unit, arrow.Radius, arrow.Height, colorTo));

            return new ModelVisual3D()
            {
                Content = model_group,
            };
        }

        public Visual3D AddLines(Point3D cubeMin, Point3D cubeMax, double thickness, Color color)
        {
            var segments = new List<(Point3D, Point3D)>();

            // Top
            segments.Add((new Point3D(cubeMin.X, cubeMin.Y, cubeMin.Z), new Point3D(cubeMax.X, cubeMin.Y, cubeMin.Z)));
            segments.Add((new Point3D(cubeMax.X, cubeMin.Y, cubeMin.Z), new Point3D(cubeMax.X, cubeMax.Y, cubeMin.Z)));
            segments.Add((new Point3D(cubeMax.X, cubeMax.Y, cubeMin.Z), new Point3D(cubeMin.X, cubeMax.Y, cubeMin.Z)));
            segments.Add((new Point3D(cubeMin.X, cubeMax.Y, cubeMin.Z), new Point3D(cubeMin.X, cubeMin.Y, cubeMin.Z)));

            // Bottom
            segments.Add((new Point3D(cubeMin.X, cubeMin.Y, cubeMax.Z), new Point3D(cubeMax.X, cubeMin.Y, cubeMax.Z)));
            segments.Add((new Point3D(cubeMax.X, cubeMin.Y, cubeMax.Z), new Point3D(cubeMax.X, cubeMax.Y, cubeMax.Z)));
            segments.Add((new Point3D(cubeMax.X, cubeMax.Y, cubeMax.Z), new Point3D(cubeMin.X, cubeMax.Y, cubeMax.Z)));
            segments.Add((new Point3D(cubeMin.X, cubeMax.Y, cubeMax.Z), new Point3D(cubeMin.X, cubeMin.Y, cubeMax.Z)));

            // Sides
            segments.Add((new Point3D(cubeMin.X, cubeMin.Y, cubeMin.Z), new Point3D(cubeMin.X, cubeMin.Y, cubeMax.Z)));
            segments.Add((new Point3D(cubeMax.X, cubeMin.Y, cubeMin.Z), new Point3D(cubeMax.X, cubeMin.Y, cubeMax.Z)));
            segments.Add((new Point3D(cubeMax.X, cubeMax.Y, cubeMin.Z), new Point3D(cubeMax.X, cubeMax.Y, cubeMax.Z)));
            segments.Add((new Point3D(cubeMin.X, cubeMax.Y, cubeMin.Z), new Point3D(cubeMin.X, cubeMax.Y, cubeMax.Z)));

            return AddLines(segments, thickness, color);
        }
        public Visual3D AddLines(IEnumerable<Point3D> points, double thickness, Color color)
        {
            var retVal = GetLines(points, thickness, color);

            Visuals3D.Add(retVal);

            return retVal;
        }
        public Visual3D AddLines(IEnumerable<(Point3D point1, Point3D point2)> lines, double thickness, Color color)
        {
            var retVal = GetLines(lines, thickness, color);

            Visuals3D.Add(retVal);

            return retVal;
        }
        public Visual3D AddLines((int i1, int i2)[] index_pairs, Point3D[] all_points, double thickness, Color color)
        {
            return AddLines(index_pairs.Select(o => (all_points[o.i1], all_points[o.i2])), thickness, color);
        }

        public static Visual3D GetLines(IEnumerable<Point3D> points, double thickness, Color color)
        {
            var segments = new List<(Point3D, Point3D)>();

            Point3D? prev = null;
            foreach (Point3D point in points)
            {
                if (prev != null)
                    segments.Add((prev.Value, point));

                prev = point;
            }

            return GetLines(segments, thickness, color);
        }
        public static Visual3D GetLines(IEnumerable<(Point3D point1, Point3D point2)> lines, double thickness, Color color)
        {
            BillboardLine3DSet visual = new BillboardLine3DSet();
            visual.Color = color;
            visual.BeginAddingLines();

            foreach (var line in lines)
                visual.AddLine(line.point1, line.point2, thickness);

            visual.EndAddingLines();

            return visual;
        }

        // These use ScreenSpaceLines3D instead of BillboardLine3D, which are constant size.  These are faster when you have lots of lines

        public Visual3D AddLines_Flat(Point3D cubeMin, Point3D cubeMax, double thickness_mult, Color color, bool autoUpdate = false)
        {
            var segments = new List<(Point3D, Point3D)>();

            // Top
            segments.Add((new Point3D(cubeMin.X, cubeMin.Y, cubeMin.Z), new Point3D(cubeMax.X, cubeMin.Y, cubeMin.Z)));
            segments.Add((new Point3D(cubeMax.X, cubeMin.Y, cubeMin.Z), new Point3D(cubeMax.X, cubeMax.Y, cubeMin.Z)));
            segments.Add((new Point3D(cubeMax.X, cubeMax.Y, cubeMin.Z), new Point3D(cubeMin.X, cubeMax.Y, cubeMin.Z)));
            segments.Add((new Point3D(cubeMin.X, cubeMax.Y, cubeMin.Z), new Point3D(cubeMin.X, cubeMin.Y, cubeMin.Z)));

            // Bottom
            segments.Add((new Point3D(cubeMin.X, cubeMin.Y, cubeMax.Z), new Point3D(cubeMax.X, cubeMin.Y, cubeMax.Z)));
            segments.Add((new Point3D(cubeMax.X, cubeMin.Y, cubeMax.Z), new Point3D(cubeMax.X, cubeMax.Y, cubeMax.Z)));
            segments.Add((new Point3D(cubeMax.X, cubeMax.Y, cubeMax.Z), new Point3D(cubeMin.X, cubeMax.Y, cubeMax.Z)));
            segments.Add((new Point3D(cubeMin.X, cubeMax.Y, cubeMax.Z), new Point3D(cubeMin.X, cubeMin.Y, cubeMax.Z)));

            // Sides
            segments.Add((new Point3D(cubeMin.X, cubeMin.Y, cubeMin.Z), new Point3D(cubeMin.X, cubeMin.Y, cubeMax.Z)));
            segments.Add((new Point3D(cubeMax.X, cubeMin.Y, cubeMin.Z), new Point3D(cubeMax.X, cubeMin.Y, cubeMax.Z)));
            segments.Add((new Point3D(cubeMax.X, cubeMax.Y, cubeMin.Z), new Point3D(cubeMax.X, cubeMax.Y, cubeMax.Z)));
            segments.Add((new Point3D(cubeMin.X, cubeMax.Y, cubeMin.Z), new Point3D(cubeMin.X, cubeMax.Y, cubeMax.Z)));

            return AddLines_Flat(segments, thickness_mult, color, autoUpdate);
        }
        public Visual3D AddLines_Flat(IEnumerable<Point3D> points, double thickness_mult, Color color, bool autoUpdate = false)
        {
            var retVal = GetLines_Flat(points, thickness_mult, color, autoUpdate);

            Visuals3D.Add(retVal);

            return retVal;
        }
        public Visual3D AddLines_Flat(IEnumerable<(Point3D point1, Point3D point2)> lines, double thickness_mult, Color color, bool autoUpdate = false)
        {
            var retVal = GetLines_Flat(lines, thickness_mult, color, autoUpdate);

            Visuals3D.Add(retVal);

            return retVal;
        }
        public Visual3D AddLines_Flat((int i1, int i2)[] index_pairs, Point3D[] all_points, double thickness_mult, Color color, bool autoUpdate = false)
        {
            return AddLines_Flat(index_pairs.Select(o => (all_points[o.i1], all_points[o.i2])), thickness_mult, color, autoUpdate);
        }

        public static Visual3D GetLines_Flat(IEnumerable<Point3D> points, double thickness_mult, Color color, bool autoUpdate = false)
        {
            var segments = new List<(Point3D, Point3D)>();

            Point3D? prev = null;
            foreach (Point3D point in points)
            {
                if (prev != null)
                    segments.Add((prev.Value, point));

                prev = point;
            }

            return GetLines_Flat(segments, thickness_mult, color, autoUpdate);
        }
        public static Visual3D GetLines_Flat(IEnumerable<(Point3D point1, Point3D point2)> lines, double thickness_mult, Color color, bool autoUpdate = false)
        {
            var visual = new ScreenSpaceLines3D(autoUpdate)
            {
                Thickness = thickness_mult,
                Color = color,
            };

            foreach (var line in lines)
                visual.AddLine(line.point1, line.point2);

            return visual;
        }

        public Visual3D AddPlane(ITriangle_wpf plane, double size, Color color, Color? reflectiveColor = null, int numCells = 12, Point3D? center = null)
        {
            var retVal = GetPlane(plane, size, color, reflectiveColor, numCells, center);

            Visuals3D.Add(retVal);

            return retVal;
        }
        public static Visual3D GetPlane(ITriangle_wpf plane, double size, Color color, Color? reflectiveColor = null, int numCells = 12, Point3D? center = null)
        {
            return new ModelVisual3D()
            {
                Content = UtilityWPF.GetPlane(plane, size, color, reflectiveColor, numCells, center),
            };
        }

        public Visual3D AddCircle(Point3D center, double radius, double thickness, Color color, ITriangle_wpf plane = null, bool isShiny = true)
        {
            var retVal = GetCircle(center, radius, thickness, color, plane, isShiny);

            Visuals3D.Add(retVal);

            return retVal;
        }
        public static Visual3D GetCircle(Point3D center, double radius, double thickness, Color color, ITriangle_wpf plane = null, bool isShiny = true)
        {
            Material material = GetMaterial(isShiny, color);

            GeometryModel3D geometry = new GeometryModel3D();
            geometry.Material = material;
            geometry.BackMaterial = material;

            geometry.Geometry = UtilityWPF.GetTorus(30, 7, thickness / 2, radius);

            Transform3DGroup transform = new Transform3DGroup();

            if (plane == null)
            {
                transform.Children.Add(new TranslateTransform3D(center.ToVector()));
            }
            else
            {
                var transform2D = Math2D.GetTransformTo2D(plane);

                // Transform the center point down to 2D
                Point3D center2D = transform2D.From3D_To2D.Transform(center);

                // Add a translate along the 2D plane
                transform.Children.Add(new TranslateTransform3D(center2D.ToVector()));

                // Now that it's positioned correctly in 2D, transform the whole thing into 3D (to line up with the 3D plane that was passed in)
                transform.Children.Add(transform2D.From2D_BackTo3D);
            }

            geometry.Transform = transform;

            return new ModelVisual3D
            {
                Content = geometry
            };
        }

        public Visual3D AddEllipse(Point3D center, Vector3D direction_x, Vector3D direction_y, double radiusX, double radiusY, double thickness, Color color, bool isShiny = true)
        {
            var retVal = GetEllipse(center, direction_x, direction_y, radiusX, radiusY, thickness, color, isShiny);

            Visuals3D.Add(retVal);

            return retVal;
        }
        public static Visual3D GetEllipse(Point3D center, Vector3D direction_x, Vector3D direction_y, double radiusX, double radiusY, double thickness, Color color, bool isShiny = true)
        {
            Material material = GetMaterial(isShiny, color);

            GeometryModel3D geometry = new GeometryModel3D();
            geometry.Material = material;
            geometry.BackMaterial = material;

            geometry.Geometry = UtilityWPF.GetTorusEllipse(30, 7, thickness / 2, radiusX, radiusY);

            Transform3DGroup transform = new Transform3DGroup();

            transform.Children.Add(new RotateTransform3D(new QuaternionRotation3D(Math3D.GetRotation(new Vector3D(1, 0, 0), new Vector3D(0, 1, 0), direction_x, direction_y))));
            transform.Children.Add(new TranslateTransform3D(center.ToVector()));

            geometry.Transform = transform;

            return new ModelVisual3D
            {
                Content = geometry
            };
        }

        /// <summary>
        /// Angles are in degrees.  Zero angle is 1,0 (90 degrees is 0,1)
        /// direction_x and y are the directions (out from center) in world space
        /// </summary>
        public Visual3D AddArc(Point3D center, Vector3D direction_x, Vector3D direction_y, double radius, double fromAngle, double toAngle, double thickness, Color color, bool isShiny = true, int num_segments = 30)
        {
            var retVal = GetArc(center, direction_x, direction_y, radius, fromAngle, toAngle, thickness, color, isShiny, num_segments);

            Visuals3D.Add(retVal);

            return retVal;
        }
        public static Visual3D GetArc(Point3D center, Vector3D direction_x, Vector3D direction_y, double radius, double fromAngle, double toAngle, double thickness, Color color, bool isShiny = true, int num_segments = 30)
        {
            Material material = GetMaterial(isShiny, color);

            GeometryModel3D geometry = new GeometryModel3D();
            geometry.Material = material;
            geometry.BackMaterial = material;

            geometry.Geometry = UtilityWPF.GetTorusArc(num_segments, 7, thickness / 2, radius, fromAngle, toAngle);

            Transform3DGroup transform = new Transform3DGroup();

            transform.Children.Add(new RotateTransform3D(new QuaternionRotation3D(Math3D.GetRotation(new Vector3D(1, 0, 0), new Vector3D(0, 1, 0), direction_x, direction_y))));
            transform.Children.Add(new TranslateTransform3D(center.ToVector()));

            geometry.Transform = transform;

            return new ModelVisual3D
            {
                Content = geometry
            };
        }

        public Visual3D[] AddTriangle(ITriangle_wpf triangle, Color? faceColor = null, Color? edgeColor = null, double? edgeThickness = null, bool isShinyFaces = true)
        {
            TriangleIndexed_wpf indexed = new TriangleIndexed_wpf(0, 1, 2, new[] { triangle.Point0, triangle.Point1, triangle.Point2 });

            return AddHull(new[] { indexed }, faceColor, edgeColor, edgeThickness, isShinyFaces, true);
        }
        public Visual3D[] AddTriangle(Point3D point1, Point3D point2, Point3D point3, Color? faceColor = null, Color? edgeColor = null, double? edgeThickness = null, bool isShinyFaces = true)
        {
            TriangleIndexed_wpf indexed = new TriangleIndexed_wpf(0, 1, 2, new[] { point1, point2, point3 });

            return AddHull(new[] { indexed }, faceColor, edgeColor, edgeThickness, isShinyFaces, true);
        }

        public Visual3D[] AddHull(ITriangleIndexed_wpf[] hull, Color? faceColor = null, Color? edgeColor = null, double? edgeThickness = null, bool isShinyFaces = true, bool isIndependentFaces = true)
        {
            var retVal = GetHull(hull, faceColor, edgeColor, edgeThickness, isShinyFaces, isIndependentFaces);

            Visuals3D.AddRange(retVal);

            return retVal;
        }
        public static Visual3D[] GetHull(ITriangleIndexed_wpf[] hull, Color? faceColor = null, Color? edgeColor = null, double? edgeThickness = null, bool isShinyFaces = true, bool isIndependentFaces = true, bool use_flat_lines = true)
        {
            var retVal = new List<Visual3D>();

            // Lines
            if (edgeColor != null && edgeThickness != null)
            {
                var hullLines = TriangleIndexed_wpf.GetUniqueLines(hull);
                var hullLinePoints = hullLines.
                    Select(o => (hull[0].AllPoints[o.Item1], hull[0].AllPoints[o.Item2]));

                if (use_flat_lines)
                    retVal.Add(GetLines_Flat(hullLinePoints, 1, edgeColor.Value));
                else
                    retVal.Add(GetLines(hullLinePoints, edgeThickness.Value, edgeColor.Value));
            }

            // Hull
            if (faceColor != null)
            {
                Material material = GetMaterial(isShinyFaces, faceColor.Value);

                GeometryModel3D geometry = new GeometryModel3D();
                geometry.Material = material;
                geometry.BackMaterial = material;

                geometry.Geometry = isIndependentFaces ?
                    UtilityWPF.GetMeshFromTriangles_IndependentFaces(hull) :
                    UtilityWPF.GetMeshFromTriangles(hull);

                retVal.Add(new ModelVisual3D
                {
                    Content = geometry
                });
            }

            return retVal.ToArray();
        }

        public Visual3D AddSquare(Point center, double sizeX, double sizeY, Color color, bool isShiny = true, double z = 0)
        {
            double halfSizeX = sizeX / 2;
            double halfSizeY = sizeY / 2;

            Point min = new Point(center.X - halfSizeX, center.Y - halfSizeY);
            Point max = new Point(center.X + halfSizeX, center.Y + halfSizeY);

            return AddSquare(min, max, color, isShiny, z);
        }
        public Visual3D AddSquare(Point min, Point max, Color color, bool isShiny = true, double z = 0)
        {
            var retVal = GetSquare(min, max, color, isShiny, z);

            Visuals3D.Add(retVal);

            return retVal;
        }
        public Visual3D AddSquare(Rect rect, Color color, bool isShiny = true, double z = 0)
        {
            return AddSquare(rect.TopLeft, rect.BottomRight, color, isShiny, z);
        }
        public static Visual3D GetSquare(Point min, Point max, Color color, bool isShiny = true, double z = 0)
        {
            Material material = GetMaterial(isShiny, color);

            return new ModelVisual3D
            {
                Content = new GeometryModel3D
                {
                    Material = material,
                    BackMaterial = material,
                    Geometry = UtilityWPF.GetSquare2D(min, max, z),
                },
            };
        }

        public Visual3D AddMesh(MeshGeometry3D mesh, Color color, bool isShinyFaces = true)
        {
            var retVal = GetMesh(mesh, color, isShinyFaces);

            Visuals3D.Add(retVal);

            return retVal;
        }
        public static Visual3D GetMesh(MeshGeometry3D mesh, Color color, bool isShinyFaces = true)
        {
            Material material = GetMaterial(isShinyFaces, color);

            GeometryModel3D geometry = new GeometryModel3D();
            geometry.Material = material;
            geometry.BackMaterial = material;

            geometry.Geometry = mesh;

            return new ModelVisual3D
            {
                Content = geometry
            };
        }

        public Visual3D AddModel(Model3D model)
        {
            var retVal = GetModel(model);

            Visuals3D.Add(retVal);

            return retVal;
        }
        public static Visual3D GetModel(Model3D model)
        {
            return new ModelVisual3D
            {
                Content = model
            };
        }

        /// <summary>
        /// This adds text into the 3D scene
        /// NOTE: This is affected by WPF's "feature" of semi transparent visuals only showing visuals that were added before
        /// </summary>
        /// <remarks>
        /// Think of the text sitting in a 2D rectangle. position=center, normal=vector sticking out of rect, textDirection=vector along x
        /// </remarks>
        /// <param name="position">The center point of the text</param>
        /// <param name="normal">The direction of the vector that points straight out of the plane of the text (default is 0,0,1)</param>
        /// <param name="textDirection">The direction of the vector that points along the text (default is 1,0,0)</param>
        public Visual3D AddText3D(string text, Point3D position, Vector3D normal, double height, Color color, bool isShiny, Vector3D? textDirection = null, double? depth = null, FontFamily font = null, FontStyle? style = null, FontWeight? weight = null, FontStretch? stretch = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var retVal = GetText3D(text, position, normal, height, color, isShiny, textDirection, depth, font ?? FontFamily, style, weight, stretch);

            Visuals3D.Add(retVal);

            return retVal;
        }
        public static Visual3D GetText3D(string text, Point3D position, Vector3D normal, double height, Color color, bool isShiny, Vector3D? textDirection = null, double? depth = null, FontFamily font = null, FontStyle? style = null, FontWeight? weight = null, FontStretch? stretch = null)
        {
            Material faceMaterial = GetMaterial(isShiny, color);
            Material edgeMaterial = GetMaterial(false, UtilityWPF.AlphaBlend(color, UtilityWPF.OppositeColor_BW(color), .75));

            ModelVisual3D visual = new ModelVisual3D();

            visual.Content = UtilityWPF.GetText3D(
                text,
                font ?? UtilityWPF.GetFont("Arial"),
                faceMaterial,
                edgeMaterial,
                height,
                depth ?? height / 15d,
                style,
                weight,
                stretch);

            Transform3DGroup transform = new Transform3DGroup();

            Quaternion quat = textDirection == null ?
                Math3D.GetRotation(new Vector3D(0, 0, 1), normal) :
                Math3D.GetRotation(new DoubleVector_wpf(new Vector3D(0, 0, 1), new Vector3D(1, 0, 0)), new DoubleVector_wpf(normal, textDirection.Value));

            transform.Children.Add(new RotateTransform3D(new QuaternionRotation3D(quat)));

            transform.Children.Add(new TranslateTransform3D(position.ToVector()));
            visual.Transform = transform;

            return visual;
        }

        /// <summary>
        /// This tells how often a value occurs
        /// </summary>
        /// <remarks>
        /// This calls func a bunch of times, finds the min/max, creates subranges, then counts the number of
        /// occurances in each range
        /// </remarks>
        public static GraphResult GetCountGraph(Func<double> func, string title = null)
        {
            double[] raw = Enumerable.Range(0, 100000).
                Select(o => func()).
                ToArray();

            double min = raw.Min();
            double max = raw.Max();
            int numSteps = 100;
            double rangeStep = (max - min) / numSteps;

            (double from, double to)[] ranges = Enumerable.Range(0, numSteps).
                Select(o => (min + (o * rangeStep), min + (o * rangeStep + rangeStep))).
                ToArray();

            int[] counts = new int[numSteps];

            //var testIndices = raw.
            //    Select(o => (value: o, index: ((o - min) / rangeStep).ToInt_Floor())).
            //    OrderBy(o => o.index).
            //    ToArray();

            int errorCount = 0;

            foreach (double rawValue in raw)
            {
                // find the bucket
                int index = ((rawValue - min) / rangeStep).ToInt_Floor();

                if (index >= numSteps)
                {
                    if (rawValue.IsNearValue(max))       // this is pretty rare, but does happen around once every 15,000 times (and least when func is rand.nextdouble())
                    {
                        index = numSteps - 1;
                    }
                    else
                    {
                        errorCount++;
                        continue;
                    }
                }

                counts[index]++;
            }

            if (errorCount > numSteps * .001)
            {
                throw new ApplicationException("There is a bug with calculating the index");
            }

            // need these as doubles to avoid integer division
            double maxCounts = counts.Max();
            double numStepsDbl = numSteps;

            var normalized = Enumerable.Range(0, numSteps).
                Select(o => new Point(o / numStepsDbl, counts[o] / maxCounts)).
                ToArray();

            return new GraphResult()
            {
                Title = title,
                Min = min,
                Max = max,
                Ranges = ranges,
                Counts = counts,
                NormalizedPoints = normalized,
            };
        }
        /// <summary>
        /// This directly graphs the values (x is the index into values, y is the value at that index)
        /// </summary>
        public static GraphResult GetGraph(double[] values, string title = null)
        {
            double maxX = values.Length;        // need a double so that it's not integer division

            double minY = values.Min();
            double maxY = values.Max();

            if (minY < 0 && maxY < 0)
                maxY = 0;

            else if (minY > 0 && maxY > 0)
                minY = 0;

            double range = maxY - minY;

            Point[] normalized = values.
                Select((o, i) => new Point
                (
                    i / maxX,
                    UtilityMath.GetScaledValue(0, 1, minY, maxY, o)
                )).
                ToArray();

            // Debug3DWindow.AddGraph() only looks at title and normalized values
            return new GraphResult()
            {
                Title = title,
                NormalizedPoints = normalized,
            };
        }

        public Visual3D[] AddGraph(GraphResult graph, Point3D center, double size, Action<GraphMouseArgs> mouseMove = null)
        {
            double halfSize = size / 2;

            Rect3D rect = new Rect3D(center.X - halfSize, center.Y - halfSize, center.Z, size, size, 0);

            return AddGraph(graph, rect, mouseMove);
        }
        public Visual3D[] AddGraph(GraphResult graph, Rect3D location, Action<GraphMouseArgs> mouseMove = null)
        {
            var retVal = new List<Visual3D>();

            double thickness = Math1D.Max(location.SizeX, location.SizeY, location.SizeZ) / 500;

            retVal.Add(AddLines(
                new[]
                {
                    // horizontal
                    (new Point3D(location.X, location.Y + (location.SizeY * .25), location.Z), new Point3D(location.X + location.SizeX, location.Y + (location.SizeY * .25), location.Z)),
                    (new Point3D(location.X, location.Y + (location.SizeY * .5), location.Z), new Point3D(location.X + location.SizeX, location.Y + (location.SizeY * .5), location.Z)),
                    (new Point3D(location.X, location.Y + (location.SizeY * .75), location.Z), new Point3D(location.X + location.SizeX, location.Y + (location.SizeY * .75), location.Z)),
                    (new Point3D(location.X, location.Y + location.SizeY, location.Z), new Point3D(location.X + location.SizeX, location.Y + location.SizeY, location.Z)),

                    // vertical
                    (new Point3D(location.X + (location.SizeX * .25), location.Y, location.Z), new Point3D(location.X  + (location.SizeX * .25), location.Y + location.SizeY, location.Z)),
                    (new Point3D(location.X + (location.SizeX * .5), location.Y, location.Z), new Point3D(location.X  + (location.SizeX * .5), location.Y + location.SizeY, location.Z)),
                    (new Point3D(location.X + (location.SizeX * .75), location.Y, location.Z), new Point3D(location.X  + (location.SizeX * .75), location.Y + location.SizeY, location.Z)),
                    (new Point3D(location.X + location.SizeX, location.Y, location.Z), new Point3D(location.X  + location.SizeX, location.Y + location.SizeY, location.Z)),

                    // diagonal
                    (new Point3D(location.X, location.Y, location.Z), new Point3D(location.X + location.SizeX, location.Y + location.SizeY, location.Z)),
                },
                thickness,
                Colors.Gray));

            retVal.Add(AddLines(
                new[]
                {
                    (new Point3D(location.X, location.Y, location.Z), new Point3D(location.X, location.Y + location.SizeY, location.Z)),
                    (new Point3D(location.X, location.Y, location.Z), new Point3D(location.X + location.SizeX, location.Y, location.Z))
                },
                thickness,
                Colors.Black));

            Point3D[] pointsWorld = graph.NormalizedPoints.
                Select(o => new Point3D
                (
                    location.X + (location.SizeX * o.X),
                    location.Y + (location.SizeY * o.Y),
                    location.Z
                )).
                ToArray();

            retVal.Add(AddLines(
                Enumerable.Range(0, graph.NormalizedPoints.Length - 1).
                    Select(o => (pointsWorld[o], pointsWorld[o + 1])),
                thickness,
                Colors.White));

            if (mouseMove != null)
                retVal.Add(AddGraphMousePlate(graph, location, mouseMove));

            if (!string.IsNullOrWhiteSpace(graph.Title))
                retVal.Add(AddText3D(graph.Title, new Point3D(location.CenterX(), location.CenterY(), location.CenterZ()), new Vector3D(0, 0, 1), location.SizeY / 20, Colors.Black, false));

            return retVal.ToArray();
        }
        public Visual3D[] AddGraphs(GraphResult[] graphs, Point3D center, double graphSize, Action<GraphMouseArgs>[] mouseMoves = null, bool showHotTrackLines = false)
        {
            if (graphs == null || graphs.Length == 0)
                return null;

            if (mouseMoves != null && mouseMoves.Length != graphs.Length)
                throw new ArgumentException($"If mouseMoves is passed in, it needs to be the same size as graphs: graphs={graphs.Length}, mouseMoves={mouseMoves.Length}");

            int rows = Math.Sqrt(graphs.Length).ToInt_Floor();

            int columns = graphs.Length / rows;
            if (graphs.Length % rows != 0)
                columns++;

            var cells = Math2D.GetCells_InvertY(graphSize * 1.1111111, columns, rows).
                ToArray();

            Action<GraphMouseArgs>[] hottrackEvents = null;
            if (showHotTrackLines)
            {
                #region hottrack lines

                List<Visual3D> lines = new List<Visual3D>();

                double thickness = Math.Max(cells[0].rect.Width, cells[0].rect.Height) / 500;

                hottrackEvents = Enumerable.Range(0, graphs.Length).
                    Select((o, i) => new Action<GraphMouseArgs>(e =>
                    {
                        if (mouseMoves != null)
                            mouseMoves[i](e);

                        Visuals3D.RemoveAll(lines);
                        lines.Clear();

                        // Horizontal
                        lines.Add(GetLines(cells.Select(p =>
                        {
                            double x = p.rect.X + (p.rect.Width * e.NormalizedPoint.X);

                            return (new Point3D(x, p.rect.Y, 0), new Point3D(x, p.rect.Y + p.rect.Height, 0));
                        }),
                        thickness,
                        SystemColors.HotTrackColor));

                        //TODO: Create a horizontal line for each graph where the graph intersects the x

                        Visuals3D.AddRange(lines);
                    })).
                    ToArray();

                #endregion
            }

            var retVal = new List<Visual3D>();

            for (int cntr = 0; cntr < graphs.Length; cntr++)
            {
                Rect cellRect = cells[cntr].rect.ChangeSize(.9);
                cellRect.Location += center.ToVector2D();

                retVal.AddRange(AddGraph(graphs[cntr], cellRect.ToRect3D(center.Z), hottrackEvents?[cntr] ?? mouseMoves?[cntr]));
            }

            return retVal.ToArray();
        }

        /// <summary>
        /// This is the same as AddMessage.  I constantly find myself trying to type AddText, but the properties are
        /// called Message
        /// </summary>
        public void AddText(string text, bool isBottom = true, string color = null)
        {
            AddMessage(text, isBottom, color);
        }
        public void AddMessage(string text, bool isBottom = true, string color = null)
        {
            #region fore color

            Color foreColor;

            if (color == null)
            {
                if (this.Background is SolidColorBrush)
                {
                    // Ignore color portion, just go black or white
                    foreColor = UtilityWPF.OppositeColor_BW(((SolidColorBrush)this.Background).Color);
                }
                else
                {
                    foreColor = Colors.Black;
                }
            }
            else
            {
                foreColor = UtilityWPF.ColorFromHex(color);
            }

            #endregion

            TextBlock textblock = new TextBlock()
            {
                Text = text,
                Foreground = new SolidColorBrush(foreColor),
            };

            if (isBottom)
            {
                this.Messages_Bottom.Add(textblock);
            }
            else
            {
                this.Messages_Top.Add(textblock);
            }
        }

        public void AddSnapshot(object snapshot, string description = null)
        {
            // Doing this now in case they change the state of the snapshot object between this call and the button getting clicked
            string serialized = XamlServices.Save(snapshot);

            Button button = new Button()
            {
                Margin = new Thickness(2),
                Content = description ?? $"snapshot {panelSnapshots.Children.Count + 1}",
            };

            button.Click += (s, e) =>
            {
                Clipboard.SetText(serialized);
                MessageBox.Show($"Serialization of {snapshot.GetType()} saved to clipboard", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            };

            panelSnapshots.Children.Add(button);
        }

        public void SetCamera(Point3D position, Vector3D lookDirection, Vector3D upDirection)
        {
            _wasSetCameraCalled = true;

            _camera.Position = position;
            _camera.LookDirection = lookDirection;
            _camera.UpDirection = upDirection;
        }

        public static Material GetMaterial(bool isShiny, Color color)
        {
            // This was copied from BillboardLine3D (then modified a bit)

            if (isShiny)
            {
                MaterialGroup retVal = new MaterialGroup();
                retVal.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
                retVal.Children.Add(new SpecularMaterial(new SolidColorBrush(UtilityWPF.ColorFromHex("40989898")), 2));

                return retVal;
            }
            else
            {
                return UtilityWPF.GetUnlitMaterial(color);
            }
        }

        public static (double dot, double line) GetDrawSizes(IEnumerable<Vector3D> vectors, Point3D? aroundCenter = null)
        {
            if (aroundCenter != null)
                return GetDrawSizes(Math.Sqrt(vectors.Max(o => (o.ToPoint() - aroundCenter.Value).LengthSquared)));
            else
                return GetDrawSizes(Math.Sqrt(vectors.Max(o => o.LengthSquared)));
        }
        public static (double dot, double line) GetDrawSizes(IEnumerable<Point3D> points, Point3D? aroundCenter = null)
        {
            if (aroundCenter != null)
                return GetDrawSizes(Math.Sqrt(points.Max(o => (o - aroundCenter.Value).LengthSquared)));
            else
                return GetDrawSizes(Math.Sqrt(points.Max(o => o.ToVector().LengthSquared)));
        }
        public static (double dot, double line) GetDrawSizes(IEnumerable<Vector> vectors, Point? aroundCenter = null)
        {
            if (aroundCenter != null)
                return GetDrawSizes(Math.Sqrt(vectors.Max(o => (o.ToPoint() - aroundCenter.Value).LengthSquared)));
            else
                return GetDrawSizes(Math.Sqrt(vectors.Max(o => o.LengthSquared)));
        }
        public static (double dot, double line) GetDrawSizes(IEnumerable<Point> points, Point? aroundCenter = null)
        {
            if (aroundCenter != null)
                return GetDrawSizes(Math.Sqrt(points.Max(o => (o - aroundCenter.Value).LengthSquared)));
            else
                return GetDrawSizes(Math.Sqrt(points.Max(o => o.ToVector().LengthSquared)));
        }
        public static (double dot, double line) GetDrawSizes(double maxRadius)
        {
            if (maxRadius.IsNearZero())
                maxRadius = 1;

            return
            (
                maxRadius * .0075,
                maxRadius * .005
            );
        }

        public List<MyHitTestResult> CastRay(System.Windows.Input.MouseEventArgs e)
        {
            return UtilityWPF.CastRay(out _, e.GetPosition(grdViewPort), grdViewPort, _camera, _viewport, true);
        }

        public void AutoSetCamera()
        {
            Point3D[] points = TryGetVisualPoints(this.Visuals3D);

            Tuple<Point3D, Vector3D, Vector3D> cameraPos = GetCameraPosition(points);      // this could return null
            if (cameraPos == null)
                cameraPos = Tuple.Create(new Point3D(0, 0, 7), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0));

            _camera.Position = cameraPos.Item1;
            _camera.LookDirection = cameraPos.Item2;
            _camera.UpDirection = cameraPos.Item3;

            double distance = _camera.Position.ToVector().Length;
            double scale = distance * .0214;

            _trackball.PanScale = scale / 10;
            _trackball.ZoomScale = scale;
            _trackball.MouseWheelScale = distance * .0007;

            RecalculateLineGeometries();
        }

        #endregion

        #region Event Listeners

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _trackball = new TrackBallRoam(_camera);
                //_trackball.KeyPanScale = 15d;
                _trackball.EventSource = grdViewPort;		//NOTE:  If this control doesn't have a background color set, the trackball won't see events (I think transparent is ok, just not null)
                _trackball.AllowZoomOnMouseWheel = true;
                _trackball.Mappings.AddRange(TrackBallMapping.GetPrebuilt(TrackBallMapping.PrebuiltMapping.MouseComplete));
                //_trackball.GetOrbitRadius += new GetOrbitRadiusHandler(Trackball_GetOrbitRadius);
                _trackball.ShouldHitTestOnOrbit = _trackball_ShouldHitTestOnOrbit;
                _trackball.InertiaPercentRetainPerSecond_Linear = _trackball_InertiaPercentRetainPerSecond_Linear;
                _trackball.InertiaPercentRetainPerSecond_Angular = _trackball_InertiaPercentRetainPerSecond_Angular;

                //TODO: May want a public bool property telling whether to auto set this.  Also do this during Visuals3D change event
                if (!_wasSetCameraCalled && this.Visuals3D.Count > 0)
                    AutoSetCamera();

                RecalculateLineGeometries();

                _loaded = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is Debug3DWindow)
                    {
                        window.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// This listener syncs Visuals3D to _viewport.  _viewport contains lights and camera, so there is an offset (that's why it's not
        /// directly bound to viewport)
        /// </summary>
        /// <remarks>
        /// http://blog.stephencleary.com/2009/07/interpreting-notifycollectionchangedeve.html
        /// </remarks>
        private void Visuals3D_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            try
            {
                #region DEBUG

                //string report = string.Format("{0}, new start={1}, new count={2}, old start={3}, old count={4} | new={5} | old={6}",
                //    e.Action,
                //    e.NewStartingIndex,
                //    e.NewItems == null ? "<null>" : e.NewItems.Count.ToString(),
                //    e.OldStartingIndex,
                //    e.OldItems == null ? "<null>" : e.OldItems.Count.ToString(),
                //    e.NewItems == null ? "<null>" : e.NewItems.AsEnumerabIe().Select(o => o.ToString()).ToJoin(";"),
                //    e.OldItems == null ? "<null>" : e.OldItems.AsEnumerabIe().Select(o => o.ToString()).ToJoin(";")
                //    );

                //AddMessage(report);

                #endregion

                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        AddVisuals(e.NewStartingIndex, e.NewItems);
                        break;

                    case NotifyCollectionChangedAction.Remove:
                        RemoveVisuals(e.OldStartingIndex, e.OldItems);
                        break;

                    case NotifyCollectionChangedAction.Reset:
                        #region Reset

                        // They did a clear.  Remove everything except the camera and lights
                        _screenspaceLines.Clear();

                        while (_viewport.Children.Count - 1 >= _viewportOffset)
                            _viewport.Children.RemoveAt(_viewport.Children.Count - 1);

                        #endregion
                        break;

                    case NotifyCollectionChangedAction.Move:
                        #region Move

                        // If Action is NotifyCollectionChangedAction.Move, then NewItems and OldItems are logically equivalent (i.e., they are
                        // SequenceEqual, even if they are different instances), and they contain the items that moved. In addition, OldStartingIndex
                        // contains the index where the items were moved from, and NewStartingIndex contains the index where the items were
                        // moved to. A Move operation is logically treated as a Remove followed by an Add, so NewStartingIndex is interpreted
                        // as though the items had already been removed.

                        RemoveVisuals(e.OldStartingIndex, e.OldItems);

                        AddVisuals(e.NewStartingIndex, e.NewItems);

                        #endregion
                        break;

                    case NotifyCollectionChangedAction.Replace:
                        #region Replace

                        //If Action is NotifyCollectionChangedAction.Replace, then OldItems contains the replaced items and NewItems contains
                        // the replacement items. In addition, NewStartingIndex and OldStartingIndex are equal, and if they are not -1, then they
                        // contain the index where the items were replaced.

                        if (e.OldStartingIndex != e.NewStartingIndex)
                            throw new ArgumentException(string.Format("e.OldStartingIndex and e.NewStartingIndex should be equal for replace: old={0}, new={1}", e.OldStartingIndex, e.NewStartingIndex));

                        else if (e.OldItems == null)
                            throw new ArgumentException("e.OldItems should never be null for replace");

                        else if (e.NewItems == null)
                            throw new ArgumentException("e.NewItems should never be null for replace");

                        RemoveVisuals(e.OldStartingIndex, e.OldItems);

                        AddVisuals(e.NewStartingIndex, e.NewItems);

                        #endregion
                        break;

                    default:
                        throw new ApplicationException("Unknown NotifyCollectionChangedAction: " + e.Action.ToString());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void grdViewPort_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                if (_graphMouseTrackers.Count > 0)
                {
                    var hits = UtilityWPF.CastRay(out _, e.GetPosition(grdViewPort), grdViewPort, _camera, _viewport, true);

                    if (hits.Count > 0)
                    {
                        foreach (var graph in _graphMouseTrackers)
                        {
                            var hit = hits.FirstOrDefault(o => o.ModelHit.VisualHit == graph.Visual);

                            //WARNING: This ignores transforms
                            if (hit != null)
                            {
                                Point3D point = hit.ModelHit.PointHit;

                                //var visualBounds = hit.ModelHit.MeshHit.Bounds;       // this seems to be the same as model's bounds
                                var visualBounds = hit.ModelHit.ModelHit.Bounds;

                                //TODO: It looks like UtilityWPF.CastRay honors model's transform, but ignores visual's transform.  But Bounds is still untransformed
                                //In order for the percent of X and Y logic below to work, the hit point would need to be reverse transformed into the original mesh's
                                //coords
                                //hit.ModelHit.ModelHit.Transform
                                //hit.ModelHit.VisualHit.Transform

                                //AddDot(point, .01, Colors.Red);

                                double percentX = (point.X - visualBounds.X) / visualBounds.SizeX;
                                double percentY = (point.Y - visualBounds.Y) / visualBounds.SizeY;

                                double graphX = UtilityMath.GetScaledValue_Capped(graph.Graph.Min, graph.Graph.Max, 0, 1, percentX);

                                graph.Delegate(new GraphMouseArgs()
                                {
                                    Graph = graph.Graph,
                                    VisualBounds = visualBounds,
                                    HitPoint = point,
                                    NormalizedPoint = new Point(percentX, percentY),
                                    SelectedXValue = graphX,
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void grdViewPort_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                RecalculateLineGeometries();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Camera_Changed(object sender, EventArgs e)
        {
            try
            {
                RecalculateLineGeometries();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Private Methods

        private void AddVisuals(int newStartingIndex, IList newItems)
        {
            if (newItems == null)
                throw new ArgumentException("e.NewItems should never be null for add");

            // if NewStartingIndex is not -1, then it contains the index where the new items were added
            int index = newStartingIndex < 0 ?
                _viewport.Children.Count :      // add
                newStartingIndex + _viewportOffset;       // insert (always after camera and lights)

            foreach (Visual3D item in newItems)
            {
                _viewport.Children.Insert(index, item);
                index++;
            }

            RememberAnyScreenSpaceLines(newItems);
        }
        private void RemoveVisuals(int oldStartingIndex, IList oldItems)
        {
            if (oldItems == null)
                throw new ArgumentException("e.OldItems should never be null for remove");

            ForgetAnyScreenSpaceLines(oldItems);

            // If Action is NotifyCollectionChangedAction.Remove, then OldItems contains the items that were removed. In addition, if OldStartingIndex is
            // not -1, then it contains the index where the old items were removed
            if (oldStartingIndex >= 0)
            {
                if (_viewport.Children.Count <= _viewportOffset + oldItems.Count)
                    throw new ApplicationException("Trying to remove more items than exists in the viewport (observable collection and viewport fell out of sync)");

                for (int cntr = 0; cntr < oldItems.Count; cntr++)
                    _viewport.Children.RemoveAt(_viewportOffset + oldStartingIndex);
            }
            else
            {
                foreach (Visual3D item in oldItems)
                    _viewport.Children.Remove(item);
            }
        }

        /// <summary>
        /// This creates a transparent visual that will catch mouse movements and raise the mouse move event
        /// </summary>
        private Visual3D AddGraphMousePlate(GraphResult graph, Rect3D location, Action<GraphMouseArgs> mouseMove)
        {
            DiffuseMaterial material = new DiffuseMaterial(Brushes.Transparent);

            ModelVisual3D visual = new ModelVisual3D
            {
                Content = new GeometryModel3D
                {
                    Material = material,
                    BackMaterial = material,
                    Geometry = UtilityWPF.GetSquare2D(location.Location.ToPoint2D(), (location.Location + location.Size.ToVector()).ToPoint2D(), location.Z),
                },
            };

            _graphMouseTrackers.Add(new GraphMouseTracker()
            {
                Graph = graph,
                Location = location,
                Visual = visual,
                Delegate = mouseMove,
            });

            Visuals3D.Add(visual);

            return visual;
        }

        private void RememberAnyScreenSpaceLines(IList items)
        {
            foreach (var item in items)
                if (item is ScreenSpaceLines3D ssline)
                    _screenspaceLines.Add(ssline);
        }
        private void ForgetAnyScreenSpaceLines(IList items)
        {
            foreach (var item in items)
                if (item is ScreenSpaceLines3D ssline)
                    _screenspaceLines.Remove(ssline);
        }

        private void RecalculateLineGeometries()
        {
            foreach (ScreenSpaceLines3D line in _screenspaceLines)
                line.CalculateGeometry();
        }

        private static Tuple<Point3D, Vector3D, Vector3D> GetCameraPosition(Point3D[] points)
        {
            if (points == null || points.Length == 0)
                return null;

            else if (points.Length == 1)
                return Tuple.Create(new Point3D(points[0].X, points[0].Y, points[0].Z + 7), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0));

            Point3D center = Math3D.GetCenter(points);

            double[] distances = points.
                Select(o => (o - center).Length).
                ToArray();

            //TODO: Use this instead
            //Math1D.Get_Average_StandardDeviation(distances);

            double avgDist = distances.Average();
            double maxDist = distances.Max();

            double threeQuarters = UtilityMath.GetScaledValue(avgDist, maxDist, 0, 1, .75);

            double cameraDist = threeQuarters * 2.5;

            // Set camera to look at center, at a distance of X times average
            return Tuple.Create(new Point3D(center.X, center.Y, center.Z + cameraDist), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0));
        }

        private static Point3D[] TryGetVisualPoints(IEnumerable<Visual3D> visuals)
        {
            IEnumerable<Point3D> retVal = new Point3D[0];

            foreach (Visual3D visual in visuals)
            {
                Point3D[] points = null;
                try
                {
                    if (visual is ModelVisual3D)
                    {
                        ModelVisual3D visualCast = (ModelVisual3D)visual;
                        points = UtilityWPF.GetPointsFromMesh(visualCast.Content);        // this throws an exception if it doesn't know what kind of model it is
                    }
                }
                catch (Exception)
                {
                    points = null;
                }

                if (points != null)
                    retVal = retVal.Concat(points);
            }

            return retVal.ToArray();
        }

        private static ArrowProps GetArrowProps(Point3D point1, Point3D point2, bool arrow_left, bool arrow_right, double thickness)
        {
            Vector3D dir_unit = (point2 - point1).ToUnit();

            double height = thickness * 4;

            Point3D p1 = arrow_left ?
                point1 + (dir_unit * height) :
                point1;

            Point3D p2 = arrow_right ?
                point2 - (dir_unit * height) :
                point2;

            return new ArrowProps()
            {
                Point1_Adjusted = p1,
                Point2_Adjusted = p2,

                Direction_Unit = dir_unit,

                Radius = thickness * 1.5,
                Height = height,
            };
        }

        private static Model3D GetArrowTip(Point3D point_from, Point3D point_to, Vector3D direction, double radius, double height, Color color)
        {
            Material material = GetMaterial(false, color);

            var rings = new List<TubeRingBase>();
            rings.Add(new TubeRingRegularPolygon(0, false, radius, radius, true));
            rings.Add(new TubeRingPoint(height, true));

            var transform = new Transform3DGroup();

            var quat = Math3D.GetRotation(new Vector3D(0, 0, 1), direction);
            transform.Children.Add(new RotateTransform3D(new QuaternionRotation3D(quat)));
            transform.Children.Add(new TranslateTransform3D(point_to.ToVector()));

            return new GeometryModel3D()
            {
                Material = material,
                BackMaterial = material,
                Geometry = UtilityWPF.GetMultiRingedTube(7, rings, true, false),
                Transform = transform,
            };
        }

        #endregion
    }

    #region class: GraphResult

    //NOTE: A lot of the properties only make sense for a count graph.  When drawing a graph, NormalizedCounts is the only property that's really needed
    public class GraphResult
    {
        public string Title { get; set; }

        public double Min { get; set; }
        public double Max { get; set; }

        public (double from, double to)[] Ranges { get; set; }

        public int[] Counts { get; set; }

        /// <summary>
        /// This runs the range from 0 to 1, and the counts from 0 to 1
        /// </summary>
        public Point[] NormalizedPoints { get; set; }
    }

    #endregion
    #region class: GraphMouseArgs

    public class GraphMouseArgs : EventArgs
    {
        public GraphResult Graph { get; set; }

        // These are the 3D values within Debug3DWindow's _viewport
        public Rect3D VisualBounds { get; set; }
        public Point3D HitPoint { get; set; }

        /// <summary>
        /// This is the 2D position within the graph, normalized from 0 to 1 (a percent of X, percent of Y)
        /// </summary>
        public Point NormalizedPoint { get; set; }
        /// <summary>
        /// This is what the percent corresponds to in the graph's horizonal axis
        /// </summary>
        public double SelectedXValue { get; set; }
    }

    #endregion
}
