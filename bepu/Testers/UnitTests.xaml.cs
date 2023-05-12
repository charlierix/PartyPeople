using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
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
