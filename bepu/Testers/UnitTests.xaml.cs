using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
using Game.Math_WPF.WPF.Viewers;
using Octree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Game.Bepu.Testers
{
    /// <summary>
    /// This is a dumping ground of unit tests that don't really have a visual
    /// </summary>
    /// <remarks>
    /// It's not a real unit tester, since it doesn't run on a schedule and try to get lots of coverage.  It's more buttons that
    /// prove functionality, but could be used to test if something broke over time
    /// 
    /// This can also be used as a refresher for how low level math functions work
    /// </remarks>
    public partial class UnitTests : Window
    {
        #region Constructor

        public UnitTests()
        {
            InitializeComponent();

            Background = SystemColors.ControlBrush;
        }

        #endregion

        #region Event Listeners

        private void Quaternions_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Random rand = StaticRandom.GetRandomForThread();

                //System.Windows.Media.Media3D.Quaternion wpf;
                //BepuUtilities.Quaternion bepu;
                //System.Numerics.Quaternion num;

                var inputs = Enumerable.Range(0, 1000).
                    Select(o => Math3D.GetRandomRotation()).
                    ToArray();

                #region are unit quaternions

                var isUnit = inputs.
                    Select(o => new
                    {
                        orig = o,
                        unit = o.ToUnit(),
                    }).
                    Where(o => !o.orig.IsNearValue(o.unit)).
                    ToArray();

                Debug.Assert(isUnit.Length == 0, "Math3D.GetRandomRotation() should be generating unit quaternions");

                #endregion

                var inputsScaled = inputs.
                    Select(o => new
                    {
                        unit = o.ToUnit(),
                        scaled = Enumerable.Range(0, 10).
                            Select(p => (o.ToQuat_numerics() * (float)rand.NextDouble(.1, 10)).ToQuat_wpf()).
                            ToArray(),
                    }).
                    ToArray();

                var eachTypeScaled = inputsScaled.
                    Select(o => new
                    {
                        wpf = o.unit,
                        wpf_s = o.scaled,

                        bepu = o.unit.ToQuat_bepu(),
                        bepu_s = o.scaled.
                            Select(p => p.ToQuat_bepu()).
                            ToArray(),

                        num = o.unit.ToQuat_numerics(),
                        num_s = o.scaled.
                            Select(p => p.ToQuat_numerics()).
                            ToArray(),
                    }).
                    ToArray();

                #region validate near

                // they are all near each other

                var validateScaled = eachTypeScaled.
                    Select(o => new
                    {
                        orig = o,
                        unit = IsNear(o.num, o.wpf, o.bepu),
                        scaled = Enumerable.Range(0, o.bepu_s.Length).
                            Select(p => IsNear(o.num_s[p], o.wpf_s[p], o.bepu_s[p])).
                            ToArray(),
                    }).
                    Select(o => new
                    {
                        o.orig,
                        o.unit,
                        o.scaled,
                        all = o.unit && o.scaled.All(p => p),
                    }).
                    ToArray();

                var validateScaledWhere = validateScaled.
                    Where(o => !o.all).
                    ToArray();

                Debug.Assert(validateScaledWhere.Length == 0, "Found a mismatch between the lengths of wpf, numerics, bepu");

                #endregion

                #region axis angle

                //NOTE: The quaternion must be a unit length.  Otherwise the returned axis and angle are invalid
                var axisangle = eachTypeScaled.
                    Select(o => new
                    {
                        o.wpf,
                        wpf_a = (axis: o.wpf.Axis, angle: o.wpf.Angle),

                        o.num,
                        num_a = o.num.GetAxisAngle(),
                    }).
                    ToArray();

                // no negatives
                var isNeg = axisangle.
                    Where(o => o.num_a.angle < 0 || o.wpf_a.angle < 0).
                    ToArray();

                var axisangle_diff = axisangle.
                    Where(o => !IsNear(o.wpf_a, o.num_a)).
                    ToArray();

                // They don't return negative angles, they just make the axis point in the other direction
                Debug.Assert(isNeg.Length == 0, "Quaternions returned negative angles");

                // It's not a direct compare, because the axis can point in the opposite direction, using an angle that's 180 degrees off
                Debug.Assert(axisangle_diff.Length == 0, "Quaternions returned different axis angle");

                #endregion

                #region validate angle

                var radians = eachTypeScaled.
                    Select(o => new
                    {
                        wpf = (float)Math1D.DegreesToRadians(o.wpf.Angle),
                        wpf_s = o.wpf_s.
                            Select(p => (float)Math1D.DegreesToRadians(p.Angle)).
                            ToArray(),

                        bepu = BepuUtilities.Quaternion.GetAngleFromQuaternion(o.bepu),
                        bepu_s = o.bepu_s.
                            Select(p => BepuUtilities.Quaternion.GetAngleFromQuaternion(p)).
                            ToArray(),

                        num = o.num.GetRadians(),
                        num_s = o.num_s.
                            Select(p => p.GetRadians()).
                            ToArray(),
                    }).
                    Select(o => new
                    {
                        orig = o,
                        //arr = new[] { o.wpf, o.bepu, o.num }.
                        arr = new[] { o.wpf, o.num }.
                            Concat(o.wpf_s).
                            //Concat(o.bepu_s).     // bepu doesn't account for scale
                            Concat(o.num_s).
                            ToArray(),
                    }).
                    ToArray();

                var radiansWhere = radians.
                    Where(o => !Math1D.IsNearValue(o.arr)).
                    ToArray();



                // this is invalid as written, can't just look at angle.  The vector could be flipped and the angle 180 degrees off





                // A quaternion isn't very useful if it's not unit length (unless there are operations that I don't know about that require different scales),
                // so this test is kind of silly
                Debug.Assert(radiansWhere.Length == 0, "Get Angle was affected by quaternion's scale");

                #endregion

                #region neg angle

                var quat_negAngle = System.Numerics.Quaternion.CreateFromAxisAngle(new Vector3(5).ToUnit(), Math1D.DegreesToRadians(-45));
                var resultingAngle = quat_negAngle.GetAxisAngle();

                Debug.Assert(resultingAngle.angle >= 0, "quaternion extension method returned a negative angle");

                #endregion

                #region rotate vectors

                var initialVectors = Enumerable.Range(0, 1000).
                    Select(o => Math3D.GetRandomVector_Spherical(10).ToVector3()).
                    ToArray();

                var rotated = eachTypeScaled.
                    Select(o => new
                    {
                        wpf = o.wpf,
                        wpf_v = initialVectors.
                            Select(p => o.wpf.GetRotatedVector(p.ToVector_wpf())).
                            ToArray(),

                        num = o.num,
                        num_v = initialVectors.
                            Select(p => o.num.GetRotatedVector(p)).
                            ToArray(),
                    }).
                    ToArray();

                var rotate_diffs = rotated.
                    Select(o => new
                    {
                        o.wpf,
                        o.num,
                        vects = Enumerable.Range(0, o.num_v.Length).
                            Select(p => new
                            {
                                wpf = o.wpf_v[p],
                                num = o.num_v[p],
                                isnear = IsNearValue(o.wpf_v[p].ToVector3(), o.num_v[p]),
                                maxdiff = GetMaxDiff(o.wpf_v[p].ToVector3(), o.num_v[p]),
                            }).
                            Where(p => !p.isnear).
                            OrderByDescending(p => p.maxdiff).
                            ToArray(),
                    }).
                    Where(o => o.vects.Length > 0).
                    OrderByDescending(o => o.vects.Length).
                    ToArray();

                Debug.Assert(rotate_diffs.Length == 0, "Comparing vectors rotated by quaternions (wpf vs num) diverged too much");

                if (rotate_diffs.Length > 0)        // this used to be populated, but max diff was up to 3e-6.  So I just loosened float epsilon to 5e-6
                {
                    var maxDif = rotate_diffs.
                        SelectMany(o => o.vects).
                        Max(o => o.maxdiff);
                }

                #endregion

                #region keep rolling

                var eachType = eachTypeScaled.
                    Select(o => (wpf: o.wpf, num: o.num)).
                    ToArray();

                for (int outer = 0; outer < 1000; outer++)
                {
                    var rot = Math3D.GetRandomRotation();

                    for (int inner = 0; inner < eachType.Length; inner++)
                    {
                        eachType[inner].wpf = eachType[inner].wpf.RotateBy(rot);
                        eachType[inner].num = eachType[inner].num.RotateBy(rot.ToQuat_numerics());
                    }
                }

                var lengths = eachType.
                    Select(o => new
                    {
                        wpf = GetLength(o.wpf),
                        num = o.num.Length()
                    }).
                    ToArray();

                var lengthsWhere = lengths.
                    Where(o => !o.wpf.IsNearValue(1) || !o.num.IsNearValue(1)).
                    ToArray();

                Debug.Assert(lengthsWhere.Length == 0, "Lots of random quat rotations caused them to drift too far from being unit length");

                #endregion

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MirrorQuat_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new Debug3DWindow();

                var sizes = Debug3DWindow.GetDrawSizes(2);

                var get_vectors = new Func<Vector3D[]>(() =>
                    new[]
                    {
                        new Vector3D(1,0,0),
                        new Vector3D(0,1,0),
                        new Vector3D(0,0,1),
                    });

                var draw_vectors = new Action<Vector3D[]>(v =>
                    {
                        window.AddLine(new Point3D(), v[0].ToPoint(), sizes.line, UtilityWPF.ColorFromHex(Debug3DWindow.AXISCOLOR_X));
                        window.AddLine(new Point3D(), v[1].ToPoint(), sizes.line, UtilityWPF.ColorFromHex(Debug3DWindow.AXISCOLOR_Y));
                        window.AddLine(new Point3D(), v[2].ToPoint(), sizes.line, UtilityWPF.ColorFromHex(Debug3DWindow.AXISCOLOR_Z));
                    });

                // Original
                var quat = Math3D.GetRandomRotation();

                var rotated_orig = get_vectors();
                quat.GetRotatedVector(rotated_orig);

                draw_vectors(rotated_orig);

                // Mirror Plane
                //var normal = Math3D.GetRandomVector_Spherical_Shell(1);       // there are special cases for the orthogonal axiis, not sure about an arbitrary vector
                var normal = UtilityCore.GetRandomEnum<Axis>();
                var normal_vec = normal switch
                {
                    Axis.X => new Vector3D(1, 0, 0),
                    Axis.Y => new Vector3D(0, 1, 0),
                    Axis.Z => new Vector3D(0, 0, 1),
                    _ => throw new ApplicationException($"Unknown Axis: {normal}"),
                };

                window.AddPlane(Math3D.GetPlane(new Point3D(), normal_vec), 1, Colors.Black);

                window.AddText(normal.ToString());

                var mirrored_quat_1 = GetMirroredQuat_1(quat, normal);
                var mirrored_quat_2 = GetMirroredQuat_2(quat, normal);
                var mirrored_quat_final = Math3D.GetMirroredRotation(quat, normal);

                if (!mirrored_quat_1.IsNearValue(mirrored_quat_2) || !mirrored_quat_1.IsNearValue(mirrored_quat_final))       // they are always the same
                {
                    MessageBox.Show("quats are different", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                var rotated_1 = get_vectors();
                mirrored_quat_1.GetRotatedVector(rotated_1);

                draw_vectors(rotated_1);

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CircularBuffer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var circular = new CircularBuffer<string>(6);

                for (int i = 0; i < 3; i++)
                {
                    if (i > 0)
                        circular.Clear();

                    int count = circular.CurrentSize;
                    var latest = circular.LastestItem;

                    var results = Enumerable.Range(0, 24).
                        Select(o =>
                        {
                            circular.Add(o.ToString());

                            return new
                            {
                                count = circular.CurrentSize,
                                latest = circular.LastestItem,

                                report = circular.
                                    GetLatestEntries().
                                    ToJoin(", "),
                            };
                        }).
                        ToArray();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AnimIndexRange_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Tiny First
                var curve = new AnimationCurve();
                curve.AddKeyValue(0, 0.76514040364407232);      // this was getting a sample count of zero, causing an exception
                curve.AddKeyValue(0.28892726277218372, 0.74394620421814894);
                curve.AddKeyValue(60, 0.57046261025833467);
                curve.AddKeyValue(120, 0.42728348034444619);
                curve.AddKeyValue(180, 0.23485959635592776);
                double test = curve.Evaluate(5);

                // Tiny Middle
                curve = new AnimationCurve();
                curve.AddKeyValue(0, 0.76514040364407232);
                curve.AddKeyValue(60, 0.57046261025833467);
                curve.AddKeyValue(60.2, 0.74394620421814894);       // this gets a count of zero
                curve.AddKeyValue(120, 0.42728348034444619);
                curve.AddKeyValue(180, 0.23485959635592776);
                test = curve.Evaluate(5);

                // Tiny Last
                curve = new AnimationCurve();
                curve.AddKeyValue(0, 0.76514040364407232);
                curve.AddKeyValue(60, 0.57046261025833467);
                curve.AddKeyValue(120, 0.42728348034444619);
                curve.AddKeyValue(180, 0.23485959635592776);
                curve.AddKeyValue(180.1, 0.74394620421814894);      // now this has the zero
                test = curve.Evaluate(5);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ToDozenal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int[] tests_int = new[]
                {
                    0,
                    1,
                    6,
                    9,
                    10,
                    11,
                    12,
                    13,
                    23,
                    24,
                    25,
                    143,
                    144,
                    145
                };

                var positive = tests_int.
                    Select(o => new { dec = o, doz = UtilityCore.Format_DecimalToDozenal(o, 0) }).
                    ToArray();

                var negative = tests_int.
                    Select(o => new { dec = -o, doz = UtilityCore.Format_DecimalToDozenal(-o, 0) }).
                    ToArray();



                double[] test_dbl = tests_int.
                    Select(o => Convert.ToDouble(o)).
                    Concat(new[]
                    {
                        0d,
                        1,
                        1.1,
                        1.5,
                        1.6,
                        1.7,
                        1.9999999999999,
                        2.00000001,
                    }).
                    Concat(Enumerable.Range(0, 12).Select(o => StaticRandom.NextDouble(48))).
                    ToArray();

                var positive2 = test_dbl.
                    Select(o => new { dec = o, doz = UtilityCore.Format_DecimalToDozenal(o, 2) }).
                    ToArray();

                var negative2 = test_dbl.
                    Select(o => new { dec = -o, doz = UtilityCore.Format_DecimalToDozenal(-o, 2) }).
                    ToArray();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CapacitorCharge_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // You can use the following equation to calculate charge based on current charge each tick:
                //
                // Q(t) = Q(0) * e ^ (-t / (RC)) + I * R * (1 - e ^ (-t / (RC)))
                //
                // where Q(t) is the charge at time t, Q(0) is the initial charge, I is the current, R is the resistance and C is the capacitance of the capacitor1

                double resistance = 1;
                double current = 1;
                double capacitance = 1;
                double deltaTime = 0.05;

                double charge = 0;

                double exp = Math.Exp(-deltaTime / (resistance * capacitance));

                var charges = new List<double>();
                charges.Add(charge);

                for (int i = 0; i < 144; i++)
                {
                    charge = charge * exp + current * resistance * (1 - exp);

                    charges.Add(charge);
                }

                var window = new Debug3DWindow();

                var graph = Debug3DWindow.GetGraph(charges.ToArray());
                window.AddGraph(graph, new Point3D(), 1);

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GuassianDropoff_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                double dt = 0.001;
                double charge = 1;

                var charges = new List<double>();
                charges.Add(charge);

                double mult = 0.5;

                for (int i = 0; i < 1000; i++)
                {
                    // derivative of e^-x^2 is -2xe^-x^2
                    double derivative = -2 * charge * Math.Exp(-charge * charge);

                    charge += derivative * mult * dt;

                    charges.Add(charge);
                }

                var window = new Debug3DWindow();

                var graph = Debug3DWindow.GetGraph(charges.ToArray());
                window.AddGraph(graph, new Point3D(), 1);

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cosine_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                double dt = 0.05;
                //double charge = 1;        // slope is zero at one
                double charge = 0.999;

                double mult = 0.1;

                var charges = new List<double>();
                charges.Add(charge);

                for (int i = 0; i < 1000; i++)
                {
                    // derivative of cos(x) is -sin(x)
                    double derivative = -Math.Sin(charge * Math.PI);

                    charge += derivative * mult * dt;

                    charges.Add(charge);
                }

                var window = new Debug3DWindow();

                var graph = Debug3DWindow.GetGraph(charges.ToArray());
                window.AddGraph(graph, new Point3D(), 1);

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Perlin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Vector3D start = Math3D.GetRandomVector_Spherical(12);
                Vector3D dir = Math3D.GetRandomVector_Spherical(3);

                double max_time = 3;
                double count = 1000;

                double[] values = Enumerable.Range(0, count.ToInt_Floor()).
                    Select(o =>
                    {
                        double time = max_time * (o / count);
                        Vector3D point = start + dir * time;
                        return Perlin.Evaluate(point.X, point.Y, point.Z);
                    }).
                    ToArray();

                var window = new Debug3DWindow() { Title = "allow negative" };

                var graph = Debug3DWindow.GetGraph(values.ToArray());
                window.AddGraph(graph, new Point3D(), 1);

                window.AddText($"dir: {dir.ToStringSignificantDigits(4)}, length: {dir.Length.ToStringSignificantDigits(4)}");

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void PerlinOctaves_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Vector3D start = Math3D.GetRandomVector_Spherical(12);
                Vector3D dir = Math3D.GetRandomVector_Spherical(3);

                int octaves = StaticRandom.Next(2, 5);

                double max_time = 3;
                double count = 1000;

                double[] values = Enumerable.Range(0, count.ToInt_Floor()).
                    Select(o =>
                    {
                        double time = max_time * (o / count);
                        Vector3D point = start + dir * time;
                        return Perlin.Octaves(point.X, point.Y, point.Z, octaves);
                    }).
                    ToArray();

                var window = new Debug3DWindow() { Title = $"octaves {octaves}" };

                var graph = Debug3DWindow.GetGraph(values.ToArray());
                window.AddGraph(graph, new Point3D(), 1);

                window.AddText($"dir: {dir.ToStringSignificantDigits(4)}, length: {dir.Length.ToStringSignificantDigits(4)}");

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void PerlinMisc2_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var start = new Vector3D(5.636, 6.568, -5.29);
                var dir = new Vector3D(1, 0, 0);

                double max_time = 3;
                double count = 36;

                string report = Enumerable.Range(0, count.ToInt_Floor()).
                    Select(o =>
                    {
                        double time = max_time * (o / count);
                        Vector3D point = start + dir * time;
                        double perlin = Perlin.Evaluate(point.X, point.Y, point.Z);

                        return $"{time} | {point.X}, {point.Y}, {point.Z} | {perlin}";
                    }).
                    ToJoin("\r\n");


                Clipboard.SetText(report);


            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //https://github.com/mcserep/NetOctree
        private void OctreePoints_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initial size (metres), initial centre position, minimum node size (metres)
                var tree = new PointOctree<Point3D>(40, new System.Numerics.Vector3(), 1);

                Point3D[] points = Enumerable.Range(0, 36).
                    Select(o => Math3D.GetRandomVector_Spherical(18).ToPoint()).
                    ToArray();

                foreach (Point3D point in points)
                {
                    tree.Add(point, point.ToVector3());
                }


                var window = new Debug3DWindow();

                var sizes = Debug3DWindow.GetDrawSizes(24);

                // Cells
                BoundingBox[] boxes = tree.GetChildBounds();

                var cell_lines = boxes.
                    SelectMany(o => Polytopes.GetCubeLines(o.Min.ToPoint_wpf(), o.Max.ToPoint_wpf())).
                    ToArray();

                var distinct_lines = Math3D.GetDistinctLineSegments(cell_lines);

                window.AddLines(distinct_lines.index_pairs, distinct_lines.all_points_distinct, sizes.line / 4, Colors.White);

                // Dots
                window.AddDots(points, sizes.dot, Colors.Black);

                // Sphere Search
                Point3D search_center = Math3D.GetRandomVector_Spherical(6).ToPoint();
                double search_radius = StaticRandom.NextDouble(3, 6);
                var hits = tree.GetNearby(search_center.ToVector3(), (float)search_radius);

                window.AddDots(hits, sizes.dot * 1.25, Colors.Chartreuse);

                window.AddDot(search_center, search_radius, UtilityWPF.ColorFromHex("1FF0"), isHiRes: true);

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void OctreeRects_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initial size (metres), initial centre position, minimum node size (metres)
                var tree = new BoundsOctree<Rect3D>(40, new System.Numerics.Vector3(), 1, 1.25f);

                Rect3D[] cells = Enumerable.Range(0, 36).
                    Select(o =>
                    {
                        Point3D center = Math3D.GetRandomVector_Spherical(18).ToPoint();
                        double size = StaticRandom.NextDouble(0.5, 3);
                        double half_size = size / 2;
                        return new Rect3D(center.X - half_size, center.Y - half_size, center.Z - half_size, size, size, size);
                    }).
                    ToArray();

                foreach (Rect3D cell in cells)
                {
                    tree.Add(cell, new BoundingBox(cell.Center().ToVector3(), cell.Size.ToVector3()));
                }


                var window = new Debug3DWindow();

                var sizes = Debug3DWindow.GetDrawSizes(24);

                // Octree Cells
                BoundingBox[] boxes = tree.GetChildBounds();

                var cell_lines = boxes.
                    SelectMany(o => Polytopes.GetCubeLines(o.Min.ToPoint_wpf(), o.Max.ToPoint_wpf())).
                    ToArray();

                var distinct_lines = Math3D.GetDistinctLineSegments(cell_lines);

                window.AddLines(distinct_lines.index_pairs, distinct_lines.all_points_distinct, sizes.line / 4, Colors.White);

                // Stored Cells
                foreach (Rect3D cell in cells)
                {
                    var cell_mesh = UtilityWPF.GetCube_IndependentFaces(cell.Location, cell.Location + cell.Size.ToVector());
                    window.AddMesh(cell_mesh, UtilityWPF.ColorFromHex("444"));
                }

                // AABB Search
                Point3D search_center = Math3D.GetRandomVector_Spherical(6).ToPoint();
                Vector3D search_size = new Vector3D
                (
                    StaticRandom.NextDouble(6, 12),
                    StaticRandom.NextDouble(6, 12),
                    StaticRandom.NextDouble(6, 12)
                );

                var aabb = Math3D.GetAABB(new[] { search_center - search_size / 2, search_center + search_size / 2 });

                var hits = tree.GetColliding(new BoundingBox(search_center.ToVector3(), search_size.ToVector3()));

                foreach (var hit in hits)
                {
                    Point3D hit_center = hit.Center();
                    Vector3D hit_size_enlarged = hit.Size.ToVector() * 1.05;     // make it slightly bigger so it goes around the black cell

                    Rect3D hit_enlarged = new Rect3D(hit_center - hit_size_enlarged / 2, hit_size_enlarged.ToSize());

                    var cell_mesh = UtilityWPF.GetCube_IndependentFaces(hit_enlarged.Location, hit_enlarged.Location + hit_enlarged.Size.ToVector());
                    window.AddMesh(cell_mesh, Colors.Chartreuse);
                }

                window.AddMesh(UtilityWPF.GetCube_IndependentFaces(aabb.min, aabb.max), UtilityWPF.ColorFromHex("1FF0"));

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClosestSegmentSegment_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Point3D[] points = Enumerable.Range(0, 4).
                    Select(o => Math3D.GetRandomVector_Spherical(4).ToPoint()).
                    ToArray();

                bool found = Math3D.GetClosestPoints_LineSegment_LineSegment(out Point3D? result1, out Point3D? result2, points[0], points[1], points[2], points[3], chkAllowBeyond.IsChecked.Value);

                var window = new Debug3DWindow();

                var sizes = Debug3DWindow.GetDrawSizes(4);

                window.AddLine(points[0], points[1], sizes.line, Colors.Black);
                window.AddLine(points[2], points[3], sizes.line, Colors.Black);

                if (found)
                {
                    window.AddDot(result1.Value, sizes.dot, Colors.DodgerBlue);
                    window.AddDot(result2.Value, sizes.dot, Colors.DodgerBlue);
                    window.AddLine(result1.Value, result2.Value, sizes.line, Colors.White);
                }

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CollidingCapsules_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Point3D[] points = Enumerable.Range(0, 4).
                    Select(o => Math3D.GetRandomVector_Spherical(4).ToPoint()).
                    ToArray();

                double[] radii = Enumerable.Range(0, 2).
                    Select(o => StaticRandom.NextDouble(0.2, 3)).
                    ToArray();

                if (StaticRandom.NextDouble() < 0.3 || (points[1] - points[0]).LengthSquared <= radii[0] * radii[0])
                    points[1] = points[0].ToVector().ToPoint();

                if (StaticRandom.NextDouble() < 0.3 || (points[3] - points[2]).LengthSquared <= radii[1] * radii[1])
                    points[3] = points[2].ToVector().ToPoint();

                var capsule1 = new Capsule_wpf()
                {
                    From = points[0],
                    To = points[1],
                    Radius = radii[0],
                    IsInterior = true,
                };

                var capsule2 = new Capsule_wpf()
                {
                    From = points[2],
                    To = points[3],
                    Radius = radii[1],
                    IsInterior = true,
                };

                bool is_intersecting = Math3D.IsIntersecting_Capsule_Capsule(capsule1, capsule2);

                var window = new Debug3DWindow();

                var sizes = Debug3DWindow.GetDrawSizes(4);

                window.AddDot(capsule1.From, sizes.dot, Colors.Black);
                window.AddDot(capsule1.To, sizes.dot, Colors.Black);

                window.AddDot(capsule2.From, sizes.dot, Colors.White);
                window.AddDot(capsule2.To, sizes.dot, Colors.White);

                string color = is_intersecting ?
                    "80CD5C5C" :
                    "8032CD32";

                var capsule1_exterior = capsule1.ToExterior();
                var capsule2_exterior = capsule2.ToExterior();

                var mesh = UtilityWPF.GetCapsule(24, 24, capsule1_exterior.From, capsule1_exterior.To, capsule1.Radius);
                window.AddMesh(mesh, UtilityWPF.ColorFromHex(color));

                mesh = UtilityWPF.GetCapsule(24, 24, capsule2_exterior.From, capsule2_exterior.To, capsule2.Radius);
                window.AddMesh(mesh, UtilityWPF.ColorFromHex(color));

                window.AddText($"1 int: {capsule1.From.ToStringSignificantDigits(2)} - {capsule1.To.ToStringSignificantDigits(2)}");
                window.AddText($"1 ext: {capsule1_exterior.From.ToStringSignificantDigits(2)} - {capsule1_exterior.To.ToStringSignificantDigits(2)}");

                window.AddText($"2 int: {capsule2.From.ToStringSignificantDigits(2)} - {capsule2.To.ToStringSignificantDigits(2)}");
                window.AddText($"2 ext: {capsule2_exterior.From.ToStringSignificantDigits(2)} - {capsule2_exterior.To.ToStringSignificantDigits(2)}");

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void IcoNormals_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // this if for a lua mod to fire raycasts in all directions uniformly
                string json = GetIcosahedronAsJson(chkIsoRandomRot.IsChecked.Value);
                Clipboard.SetText(json.ToString());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void IcoNormals2_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Directory.Exists(txtFolder.Text))
                {
                    MessageBox.Show("Please select a folder", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // this if for a lua mod to fire raycasts in all directions uniformly
                for (int i = 0; i < 12; i++)
                {
                    string json = GetIcosahedronAsJson(true);

                    string filename = System.IO.Path.Combine(txtFolder.Text, $"icosahedron {i}.json");
                    File.WriteAllText(filename, json);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DrawCapsuleZ_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new Debug3DWindow()
                {
                    Title = "Capsule Along Z",
                };

                var sizes = Debug3DWindow.GetDrawSizes(1);

                window.AddAxisLines(1, sizes.line);

                var mesh = UtilityWPF.GetCapsule_AlongZ(24, 24, 1, 4);

                window.AddMesh(mesh, UtilityWPF.ColorFromHex("801E90FF"));

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void DrawCapsule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new Debug3DWindow()
                {
                    Title = "Capsule",
                };

                var sizes = Debug3DWindow.GetDrawSizes(1);

                window.AddAxisLines(1, sizes.line);

                Point3D point0 = Math3D.GetRandomVector_Spherical(6).ToPoint();
                Point3D point1 = Math3D.GetRandomVector_Spherical(6).ToPoint();

                window.AddDot(point0, sizes.dot * 6, Colors.Black);
                window.AddDot(point1, sizes.dot * 6, Colors.Black);

                var mesh = UtilityWPF.GetCapsule(24, 24, point0, point1, 1);

                window.AddMesh(mesh, UtilityWPF.ColorFromHex("801E90FF"));

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void DrawCylinderZ_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new Debug3DWindow()
                {
                    Title = "Cylinder Along Z",
                };

                var sizes = Debug3DWindow.GetDrawSizes(1);

                window.AddAxisLines(1, sizes.line);

                var mesh = UtilityWPF.GetCylinder_AlongZ(24, 1, 4);

                window.AddMesh(mesh, UtilityWPF.ColorFromHex("801E90FF"));

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void DrawCylinder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new Debug3DWindow()
                {
                    Title = "Cylinder",
                };

                var sizes = Debug3DWindow.GetDrawSizes(1);

                window.AddAxisLines(1, sizes.line);

                Point3D point0 = Math3D.GetRandomVector_Spherical(6).ToPoint();
                Point3D point1 = Math3D.GetRandomVector_Spherical(6).ToPoint();

                window.AddDot(point0, sizes.dot * 6, Colors.Black);
                window.AddDot(point1, sizes.dot * 6, Colors.Black);

                var mesh = UtilityWPF.GetCylinder(24, point0, point1, 1);

                window.AddMesh(mesh, UtilityWPF.ColorFromHex("801E90FF"));

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Private Methods

        private static bool IsNear(System.Numerics.Quaternion num, System.Windows.Media.Media3D.Quaternion wpf, BepuUtilities.Quaternion bepu)
        {
            float[] numA = new[] { num.X, num.Y, num.Z, num.W };
            float[] wpfA = new[] { (float)wpf.X, (float)wpf.Y, (float)wpf.Z, (float)wpf.W };
            float[] bepuA = new[] { bepu.X, bepu.Y, bepu.Z, bepu.W };

            for (int cntr = 0; cntr < 4; cntr++)
            {
                if (!numA[cntr].IsNearValue(wpfA[cntr]))
                {
                    return false;
                }

                if (!numA[cntr].IsNearValue(bepuA[cntr]))
                {
                    return false;
                }

                if (!wpfA[cntr].IsNearValue(bepuA[cntr]))       // this should be unnecessary, but feels complete
                {
                    return false;
                }
            }

            return true;
        }
        private static bool IsNear_TOOSTRICT((Vector3D axis, double angle) wpf, (Vector3 axis, float angle) num)
        {
            if (wpf.angle < 0 || num.angle < 0)
            {
                throw new ApplicationException("handle negative angles");
            }

            float sub_pos = Math.Abs((float)wpf.angle - num.angle);

            if (Math1D.IsNearValue((float)wpf.angle, num.angle) && wpf.axis.ToVector3().IsNearValue(num.axis))
            {
                return true;
            }

            Vector3D negAxis = wpf.axis * -1d;
            double negAngle = 360d - wpf.angle;

            float sub_neg = Math.Abs((float)negAngle - num.angle);

            if (Math1D.IsNearValue((float)negAngle, num.angle) && negAxis.ToVector3().IsNearValue(num.axis))
            {
                return true;
            }

            return false;
        }
        private static bool IsNear((Vector3D axis, double angle) wpf, (Vector3 axis, float angle) num)
        {
            const float EPS = .5f;

            if (wpf.angle < 0 || num.angle < 0)
            {
                throw new ApplicationException("handle negative angles");
            }

            float sub_pos = Math.Abs((float)wpf.angle - num.angle);

            if (IsNearValue((float)wpf.angle, num.angle, EPS))
            {
                if (IsNearValue(wpf.axis.ToVector3(), num.axis, EPS))
                {
                    return true;
                }
            }

            Vector3D negAxis = wpf.axis * -1d;
            double negAngle = 360d - wpf.angle;

            float sub_neg = Math.Abs((float)negAngle - num.angle);

            if (IsNearValue((float)negAngle, num.angle, EPS))
            {
                if (IsNearValue(negAxis.ToVector3(), num.axis, EPS))
                {
                    return true;
                }
            }

            return false;
        }
        private static bool IsNearValue(float testValue, float compareTo, float epsilon = Math1D.NEARZERO_F)
        {
            return testValue >= compareTo - epsilon && testValue <= compareTo + epsilon;
        }
        private static bool IsNearValue(Vector3 vector, Vector3 compare, float epsilon = Math1D.NEARZERO_F)
        {
            return IsNearValue(vector.X, compare.X, epsilon) &&
                        IsNearValue(vector.Y, compare.Y, epsilon) &&
                        IsNearValue(vector.Z, compare.Z, epsilon);
        }

        private static float GetMaxDiff(Vector3 vector, Vector3 compare)
        {
            return Math1D.Max
            (
                Math.Abs(vector.X - compare.X),
                Math.Abs(vector.Y - compare.Y),
                Math.Abs(vector.Z - compare.Z)
            );
        }

        private static double GetLength(System.Windows.Media.Media3D.Quaternion quat)
        {
            return Math.Sqrt((quat.X * quat.X) + (quat.Y * quat.Y) + (quat.Z * quat.Z) + (quat.W * quat.W));
        }

        // Both of these are the same
        private static System.Windows.Media.Media3D.Quaternion GetMirroredQuat_1(System.Windows.Media.Media3D.Quaternion quat, Axis normal)
        {
            //https://stackoverflow.com/questions/32438252/efficient-way-to-apply-mirror-effect-on-quaternion-rotation

            if (quat.IsIdentity)
                return quat;

            Vector3D axis = quat.Axis;
            double angle = quat.Angle;

            switch (normal)
            {
                case Axis.X:
                    return new System.Windows.Media.Media3D.Quaternion(new Vector3D(-axis.X, axis.Y, axis.Z), -angle);

                case Axis.Y:
                    return new System.Windows.Media.Media3D.Quaternion(new Vector3D(axis.X, -axis.Y, axis.Z), -angle);

                case Axis.Z:
                    return new System.Windows.Media.Media3D.Quaternion(new Vector3D(axis.X, axis.Y, -axis.Z), -angle);

                default:
                    throw new ApplicationException($"Unknown Axis: {normal}");
            }
        }
        private static System.Windows.Media.Media3D.Quaternion GetMirroredQuat_2(System.Windows.Media.Media3D.Quaternion quat, Axis normal)
        {
            //https://stackoverflow.com/questions/32438252/efficient-way-to-apply-mirror-effect-on-quaternion-rotation

            if (quat.IsIdentity)
                return quat;

            Vector3D axis = quat.Axis;
            double angle = quat.Angle;

            switch (normal)
            {
                case Axis.X:
                    return new System.Windows.Media.Media3D.Quaternion(new Vector3D(axis.X, -axis.Y, -axis.Z), angle);

                case Axis.Y:
                    return new System.Windows.Media.Media3D.Quaternion(new Vector3D(-axis.X, axis.Y, -axis.Z), angle);

                case Axis.Z:
                    return new System.Windows.Media.Media3D.Quaternion(new Vector3D(-axis.X, -axis.Y, axis.Z), angle);

                default:
                    throw new ApplicationException($"Unknown Axis: {normal}");
            }
        }

        private static void GenerateTestPoints()
        {
            var get_num = new Func<string>(() => StaticRandom.NextDouble(-12, 12).ToStringSignificantDigits(4));

            var inputs = Enumerable.Range(0, 12).
                Select(o => $"{{ x = {get_num()}, y = {get_num()}, z = {get_num()} }},").
                ToJoin("\r\n");

            Clipboard.SetText(inputs);
        }

        private static string GetIcosahedronAsJson(bool random_rotation)
        {
            var ico = Polytopes.GetIcosahedron(0.25).       // 0 recursions is 20 faces, 1 recursion is 80
                Select(o => new Triangle_wpf(o.Point0, o.Point1, o.Point2)).
                ToArray();

            if (random_rotation)
            {
                var quat = Math3D.GetRandomRotation();

                ico = ico.
                    Select(o => new Triangle_wpf
                    (
                        quat.GetRotatedVector(o.Point0.ToVector()).ToPoint(),
                        quat.GetRotatedVector(o.Point1.ToVector()).ToPoint(),
                        quat.GetRotatedVector(o.Point2.ToVector()).ToPoint()
                    )).
                    ToArray();
            }

            var json = new StringBuilder();

            json.AppendLine("{");
            json.AppendLine("\t\"ico\": [");

            for (int i = 0; i < ico.Length; i++)
            {
                json.AppendLine("\t\t{");

                var pos = ico[i].GetCenterPoint().ToVector3();
                var norm = ico[i].NormalUnit.ToVector3();

                json.AppendLine($"\t\t\t\"pos_x\": {pos.X}, \"pos_y\": {pos.Y}, \"pos_z\": {pos.Z},");
                json.AppendLine($"\t\t\t\"norm_x\": {norm.X}, \"norm_y\": {norm.Y}, \"norm_z\": {norm.Z}");

                string comma = i < ico.Length - 1 ?
                    "," :
                    "";

                json.AppendLine("\t\t}" + comma);
            }

            json.AppendLine("\t]");
            json.AppendLine("}");

            return json.ToString();
        }

        #endregion

        #region Dual Cosine vs Bezier

        private void CosTest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                float boom_length = 1f;
                float boom_mid_length = 0.7f;

                float boom_chord_base = 0.5f;
                float boom_chord_mid = 0.2f;
                float boom_chord_tip = 0.3f;

                var window = new Debug3DWindow();

                //var sizes = Debug3DWindow.GetDrawSizes(1);

                var graphs = new List<GraphResult>();

                int n = 1000;

                #region standard cos

                var standard_cos = Enumerable.Range(0, n).
                    Select(o =>
                    {
                        double x = (double)o / (double)n;
                        double y = 0.5 + 0.5 * Math.Cos(x * 2 * Math.PI);

                        return y;
                    }).
                    ToArray();

                graphs.Add(Debug3DWindow.GetGraph(standard_cos, "standard cosine"));

                #endregion
                #region standard sin

                var standard_sin = Enumerable.Range(0, n).
                    Select(o =>
                    {
                        double x = (double)o / (double)n;
                        double y = 0.5 + 0.5 * Math.Sin(x * 2 * Math.PI);

                        return y;
                    }).
                    ToArray();

                graphs.Add(Debug3DWindow.GetGraph(standard_sin, "standard sine"));

                #endregion

                #region left side - FAIL

                //var left_side = Enumerable.Range(0, n).
                //    Select(o =>
                //    {
                //        double x = (double)o / (double)n;
                //        x *= boom_mid_length / boom_length;

                //        double y = 0.5 + 0.5 * Math.Cos(x * 2 * Math.PI);

                //        return y;
                //    }).
                //    ToArray();

                //graphs.Add(Debug3DWindow.GetGraph(left_side, "left side"));

                #endregion
                #region right side - FAIL

                //var right_side = Enumerable.Range(0, n).
                //    Select(o =>
                //    {
                //        double x = (double)o / (double)n;
                //        x *= boom_length - (boom_mid_length / boom_length);

                //        double y = 0.5 + 0.5 * Math.Sin(x * 2 * Math.PI);

                //        return y;
                //    }).
                //    ToArray();

                //graphs.Add(Debug3DWindow.GetGraph(right_side, "right side"));

                #endregion

                float t = boom_mid_length / boom_length;
                float u = 1 - t;

                #region left side

                var left_side = Enumerable.Range(0, n).
                    Select(o =>
                    {
                        double x = (double)o / (double)n;
                        double y = 0.5 + 0.5 * Math.Cos(x / t * Math.PI);

                        return y;
                    }).
                    ToArray();

                graphs.Add(Debug3DWindow.GetGraph(left_side, "left side"));

                #endregion
                #region right side

                var right_side = Enumerable.Range(0, n).
                    Select(o =>
                    {
                        double x = (double)o / (double)n;
                        double y = 0.5 + 0.5 * Math.Cos(Math.PI + x / u * Math.PI);

                        return y;
                    }).
                    ToArray();

                graphs.Add(Debug3DWindow.GetGraph(right_side, "right side"));

                #endregion

                #region combined - const height

                var comb_const = Enumerable.Range(0, n).
                    Select(o =>
                    {
                        double x = (double)o / (double)n;

                        double y;
                        if (x <= t)
                        {
                            y = 0.5 + 0.5 * Math.Cos(x / t * Math.PI);
                        }
                        else
                        {
                            y = 0.5 + 0.5 * Math.Cos(Math.PI + (x - t) / u * Math.PI);
                        }

                        return y;
                    }).
                    ToArray();

                graphs.Add(Debug3DWindow.GetGraph(comb_const, "combined - const height"));

                #endregion
                #region combined - variable height

                var comb_var = Enumerable.Range(0, n).
                    Select(o =>
                    {
                        double x = (double)o / (double)n;
                        double y = GetY_Cos(x, t, boom_chord_base, boom_chord_mid, boom_chord_tip);

                        return y;
                    }).
                    ToArray();

                graphs.Add(Debug3DWindow.GetGraph(comb_var, "combined - variable height"));

                #endregion

                #region bezier - variable height

                var bezier = BezierUtil.GetBezierSegments(new[] { new Point3D(0, boom_chord_base, 0), new Point3D(t, boom_chord_mid, 0), new Point3D(1, boom_chord_tip, 0) }, 0.1);

                var bezier_var = Enumerable.Range(0, n).
                    Select(o =>
                    {
                        double x = (double)o / (double)n;
                        Point3D point = BezierUtil.GetPoint(x, bezier);

                        return point.Y;
                    }).
                    ToArray();

                graphs.Add(Debug3DWindow.GetGraph(bezier_var, "bezier - variable height"));

                #endregion

                boom_chord_base = 0.5f;
                boom_chord_mid = 0.7f; //0.2f;
                boom_chord_tip = 0.3f;

                #region combined - variable height

                comb_var = Enumerable.Range(0, n).
                    Select(o =>
                    {
                        double x = (double)o / (double)n;
                        double y = GetY_Cos(x, t, boom_chord_base, boom_chord_mid, boom_chord_tip);

                        return y;
                    }).
                    ToArray();

                graphs.Add(Debug3DWindow.GetGraph(comb_var, "combined - variable height 2"));

                #endregion
                #region bezier - variable height

                bezier = BezierUtil.GetBezierSegments(new[] { new Point3D(0, boom_chord_base, 0), new Point3D(t, boom_chord_mid, 0), new Point3D(1, boom_chord_tip, 0) }, 0.1);

                bezier_var = Enumerable.Range(0, n).
                    Select(o =>
                    {
                        double x = (double)o / (double)n;
                        Point3D point = BezierUtil.GetPoint(x, bezier);

                        return point.Y;
                    }).
                    ToArray();

                graphs.Add(Debug3DWindow.GetGraph(bezier_var, "bezier - variable height 2"));

                #endregion

                window.AddGraphs(graphs.ToArray(), new Point3D(), 1);

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void CosTest2_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Random rand = StaticRandom.GetRandomForThread();

                var window = new Debug3DWindow();

                //var sizes = Debug3DWindow.GetDrawSizes(1);

                var graphs = new List<GraphResult>();

                int n = 1000;

                for (int i = 0; i < 12; i++)
                {
                    double y_at_0 = rand.NextDouble();
                    double y_at_mid = rand.NextDouble();
                    double y_at_1 = rand.NextDouble();

                    double t = rand.NextDouble(0.1, 0.9);
                    double u = 1 - t;

                    double bezier_along = 0.2;      //rand.NextDouble(0.05, 0.4);

                    string report = $"{t.ToStringSignificantDigits(2)}  |  {y_at_0.ToStringSignificantDigits(2)}, {y_at_mid.ToStringSignificantDigits(2)}, {y_at_1.ToStringSignificantDigits(2)}";

                    #region combined - variable height

                    var cosine = Enumerable.Range(0, n).
                        Select(o =>
                        {
                            double x = (double)o / (double)n;
                            double y = GetY_Cos(x, t, y_at_0, y_at_mid, y_at_1);

                            return y;
                        }).
                        ToArray();

                    graphs.Add(Debug3DWindow.GetGraph(cosine, $"cosine {report}"));

                    #endregion
                    #region bezier - variable height

                    var bezier = BezierUtil.GetBezierSegments(new[] { new Point3D(0, y_at_0, 0), new Point3D(t, y_at_mid, 0), new Point3D(1, y_at_1, 0) }, bezier_along);

                    var bezier_var = Enumerable.Range(0, n).
                        Select(o =>
                        {
                            double x = (double)o / (double)n;
                            Point3D point = BezierUtil.GetPoint(x, bezier);

                            return point.Y;
                        }).
                        ToArray();

                    graphs.Add(Debug3DWindow.GetGraph(bezier_var, $"bezier {report}"));

                    #endregion
                }

                window.AddGraphs(graphs.ToArray(), new Point3D(), 1);

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static double GetY_Cos(double x, double mid_x, double y_at_0, double y_at_mid, double y_at_1)
        {
            if (x <= mid_x)
            {
                double height = (y_at_0 - y_at_mid) / 2;

                return y_at_mid + height + (height * Math.Cos(x / mid_x * Math.PI));
            }
            else
            {
                double height = (y_at_1 - y_at_mid) / 2;

                return y_at_mid + height + (height * Math.Cos(Math.PI + (x - mid_x) / (1 - mid_x) * Math.PI));
            }
        }

        private void BezierPlot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                float Boom_Length = 1f;

                float Boom_Mid_Length = 0.7f;

                float Boom_Span_Base = 0.5f;
                float Boom_Span_Mid = 0.2f;
                float Boom_Span_Tip = 0.3f;

                float Boom_Bezier_PinchPercent = 0.2f;       // 0 to 0.4 (affects the curviness of the bezier, 0 is linear)


                float val_base = Boom_Span_Base;
                float val_mid = Boom_Span_Mid;
                float val_tip = Boom_Span_Tip;
                float mid_dist = Boom_Mid_Length;
                float total_span = Boom_Length;
                float pinch_percent = Boom_Bezier_PinchPercent;

                var slice0 = new[]
                {
                    new Point3D(0, 0, val_base),
                    new Point3D(mid_dist, 0, val_mid),
                    new Point3D(total_span, 0, val_tip)
                };
                var slice1 = slice0.
                    Select(o => new Point3D(o.X, 1, o.Z)).
                    ToArray();

                var bezier0 = BezierUtil.GetBezierSegments(slice0, pinch_percent);
                var bezier1 = BezierUtil.GetBezierSegments(slice1, pinch_percent);

                float[] sample_xs = new[] { 0, 1f / 3f, 2f / 3f, 1 };

                // doesn't work, x isn't walked linearly
                //var points1D = BezierUtil.GetBezierMesh_Points(new[] { bezier0, bezier1}, sample_xs.Length, 2);
                //var points2D = BezierUtil.GetBezierMesh_Horizontals(new[] { bezier0, bezier1 }, sample_xs.Length, 2);


                var get_x_at_percent = new Func<double, double>(perc => BezierUtil.GetPoint(perc, bezier0).X);


                foreach (float x in sample_xs)
                {
                    double percent = Math1D.GetInputForDesiredOutput_PosInput_PosCorrelation(x, 0.01, get_x_at_percent);

                    Point3D point = BezierUtil.GetPoint(percent, bezier0);



                }





            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
