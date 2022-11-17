using Game.Core;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.Mathematics
{
    /// <summary>
    /// Takes a set of key/value pairs (both floats) during setup.  Then at runtime will return an interpolated
    /// value for any input within range
    /// </summary>
    /// <remarks>
    /// This is inspired by unity's AnimationCurve
    /// https://docs.unity3d.com/ScriptReference/AnimationCurve.html
    /// 
    /// Used this discussion to help implement
    /// https://answers.unity.com/questions/464782/t-is-the-math-behind-animationcurveevaluate.html
    /// </remarks>
    public class AnimationCurve
    {
        private record Derived
        {
            public BezierSegment3D_wpf[] Bezier { get; init; }

            public (double key, double value)[] Bezier_Samples { get; init; }

            public double Min_Key { get; init; }
            public double Max_Key { get; init; }
            public double Min_Value { get; init; }
            public double Max_Value { get; init; }
        };

        private KeyValuePair<double, double>[] _keyvalues = new KeyValuePair<double, double>[0];

        private Derived _derived = null;

        public (double key, double value)[] KeyValues => _keyvalues.Select(o => (o.Key, o.Value)).ToArray();

        public double Min_Key => EnsureDerivedCreated().Min_Key;
        public double Max_Key => EnsureDerivedCreated().Max_Key;
        public double Min_Value => EnsureDerivedCreated().Min_Value;
        public double Max_Value => EnsureDerivedCreated().Max_Value;

        public int NumPoints => _keyvalues.Length;

        public BezierSegment3D_wpf[] Bezier => EnsureDerivedCreated().Bezier;

        public void AddKeyValue(double key, double value)
        {
            _keyvalues = _keyvalues.
                Concat(new[] { new KeyValuePair<double, double>(key, value) }).
                OrderBy(o => o.Key).
                ToArray();

            _derived = null;
        }

        public double Evaluate(double key)
        {
            if (_keyvalues.Length == 0)
                return 0;

            EnsureDerivedCreated();

            //TODO: these should extend a ray from the first/last segment of the bezier (this will fail if the bezier is too curvy and points back)
            if (key <= _keyvalues[0].Key)
                return _keyvalues[0].Value;

            if (key >= _keyvalues[^1].Key)
                return _keyvalues[^1].Value;

            //NOTE: If the curve is too wild, there will be multiple spots with the same X coords.  ex, the curve around the pipe loops back: _/|/
            //I can't think of a good way to fix this, I think it's just a consequence of trying to map to xy coords

            for (int i = 1; i < _derived.Bezier_Samples.Length; i++)
            {
                if (key > _derived.Bezier_Samples[i].key)
                    continue;

                // get the percent from prev to next key
                double percent = UtilityMath.GetScaledValue(0, 1, _derived.Bezier_Samples[i - 1].key, _derived.Bezier_Samples[i].key, key);

                // get the corresponding value along the two points
                return UtilityMath.LERP(_derived.Bezier_Samples[i - 1].value, _derived.Bezier_Samples[i].value, percent);
            }

            throw new ApplicationException($"Didn't find key: {key} | {_derived.Bezier_Samples.Select(o => o.key.ToString()).ToJoin(", ")}");
        }

        private Derived EnsureDerivedCreated()
        {
            if (_derived == null)
                _derived = BuildDerived(_keyvalues);

            return _derived;
        }
        private static Derived BuildDerived(KeyValuePair<double, double>[] keyvalues)
        {
            if (keyvalues.Length == 0)
            {
                return new Derived()
                {
                    Bezier = new BezierSegment3D_wpf[0],
                    Bezier_Samples = new (double key, double value)[0],
                    Min_Key = 0,
                    Max_Key = 0,
                    Min_Value = 0,
                    Max_Value = 0,
                };
            }
            else if (keyvalues.Length == 1)
            {
                return new Derived()
                {
                    Bezier = new BezierSegment3D_wpf[0],
                    Bezier_Samples = new[] { (keyvalues[0].Key, keyvalues[0].Value) },
                    Min_Key = keyvalues[0].Key,
                    Max_Key = keyvalues[0].Key,
                    Min_Value = keyvalues[0].Value,
                    Max_Value = keyvalues[0].Value,
                };
            }
            else if (keyvalues.Length == 2)
            {
                return new Derived()
                {
                    Bezier = new[] { new BezierSegment3D_wpf(new Point3D(keyvalues[0].Key, keyvalues[0].Value, 0), new Point3D(keyvalues[1].Key, keyvalues[1].Value, 0), new Point3D[0]) },
                    Bezier_Samples = new[] { (keyvalues[0].Key, keyvalues[0].Value), (keyvalues[1].Key, keyvalues[1].Value) },
                    Min_Key = keyvalues[0].Key,
                    Max_Key = keyvalues[1].Key,
                    Min_Value = keyvalues[0].Value,
                    Max_Value = keyvalues[1].Value,
                };
            }

            var bezier = BuildBezier(keyvalues);

            return new Derived()
            {
                Bezier = bezier,

                Bezier_Samples = BuildBezierSamples(keyvalues, bezier),

                Min_Key = keyvalues.Min(o => o.Key),
                Max_Key = keyvalues.Max(o => o.Key),
                Min_Value = keyvalues.Min(o => o.Value),
                Max_Value = keyvalues.Max(o => o.Value),
            };
        }
        private static BezierSegment3D_wpf[] BuildBezier(KeyValuePair<double, double>[] keyvalues)
        {
            Point3D[] points = keyvalues.
                Select(o => new Point3D(o.Key, o.Value, 0)).
                ToArray();

            return BezierUtil.GetBezierSegments(points, 0.12);
        }
        private static (double key, double value)[] BuildBezierSamples(KeyValuePair<double, double>[] keyvalues, BezierSegment3D_wpf[] bezier)
        {
            double total_len_keys = keyvalues[^1].Key - keyvalues[0].Key;

            // Find the closest distance between keys
            var key_distances = Enumerable.Range(0, keyvalues.Length - 1).
                Select(o => keyvalues[o + 1].Key - keyvalues[o].Key).       // the list is already sorted
                OrderBy(o => o).
                Take(1).
                ToArray();

            // Get more samples than the keyvalues
            int count = Math.Min((total_len_keys / key_distances[0]) * 16, 144).ToInt_Ceiling();

            return BezierUtil.GetPoints_UniformDistribution(count, bezier).
                Select(o => (o.X, o.Y)).
                ToArray();
        }
    }
}
