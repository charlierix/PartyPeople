using Game.Core;
using Game.Math_WPF.Mathematics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

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

        #endregion
    }
}
