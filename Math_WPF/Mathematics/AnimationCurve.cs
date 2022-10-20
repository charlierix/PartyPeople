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

            public double[] PercentsAlong { get; init; }
            public (double key, double percent)[] PercentsAlong2 { get; init; }

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

            if (key <= _keyvalues[0].Key)
                return _keyvalues[0].Value;

            if (key >= _keyvalues[^1].Key)
                return _keyvalues[^1].Value;

            EnsureDerivedCreated();

            for (int i = 1; i < _keyvalues.Length; i++)
            {
                if (key > _keyvalues[i].Key)
                    continue;

                // Can't use percent of just this segment.  Need the percent across the whole curve, then use the overload that takes array of segments
                //double percent = UtilityMath.GetScaledValue_Capped(0, 1, _keyvalues[i - 1].Key, _keyvalues[i].Key, key);
                //return BezierUtil.GetPoint(percent, _derived.Bezier[i - 1]).Y;

                double percent = UtilityMath.GetScaledValue_Capped(_derived.PercentsAlong[i - 1], _derived.PercentsAlong[i], _keyvalues[i - 1].Key, _keyvalues[i].Key, key);

                return BezierUtil.GetPoint(percent, _derived.Bezier).Y;
            }

            throw new ApplicationException($"Didn't find key: {key} | {_keyvalues.Select(o => o.Key.ToString()).ToJoin(", ")}");
        }
        public double Evaluate2(double key)
        {
            if (_keyvalues.Length == 0)
                return 0;

            if (key <= _keyvalues[0].Key)
                return _keyvalues[0].Value;

            if (key >= _keyvalues[^1].Key)
                return _keyvalues[^1].Value;

            EnsureDerivedCreated();

            for (int i = 1; i < _derived.PercentsAlong2.Length; i++)
            {
                if (key > _derived.PercentsAlong2[i].key)
                    continue;

                double percent = UtilityMath.GetScaledValue_Capped(_derived.PercentsAlong2[i - 1].percent, _derived.PercentsAlong2[i].percent, _derived.PercentsAlong2[i - 1].key, _derived.PercentsAlong2[i].key, key);

                return BezierUtil.GetPoint(percent, _derived.Bezier).Y;
            }

            throw new ApplicationException($"Didn't find key: {key} | {_keyvalues.Select(o => o.Key.ToString()).ToJoin(", ")}");
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
                    PercentsAlong = new double[0],
                    PercentsAlong2 = new (double, double)[0],
                    Min_Key = 0,
                    Max_Key = 0,
                    Min_Value = 0,
                    Max_Value = 0,
                };
            }

            var bezier = BuildBezier(keyvalues);
            //var bezier = BuildBezier_ALT(keyvalues);      // this was just an experiment to see what it looks like

            return new Derived()
            {
                Bezier = bezier,

                PercentsAlong = BuildPercentsAlong(keyvalues),
                PercentsAlong2 = BuildPercentsAlong4(keyvalues, bezier),

                Min_Key = keyvalues.Min(o => o.Key),
                Max_Key = keyvalues.Max(o => o.Key),
                Min_Value = keyvalues.Min(o => o.Value),
                Max_Value = keyvalues.Max(o => o.Value),
            };
        }

        //TODO: this is too simple.  Need to approximate the length of the curve, then see where each point is along that path
        //Also, don't tie the length of the return to the number of keyvalues.  Have more, and evaluate function will need to walk this list
        private static double[] BuildPercentsAlong(KeyValuePair<double, double>[] keyvalues)
        {
            double length = keyvalues[^1].Key - keyvalues[0].Key;

            return keyvalues.
                Select(o => (o.Key - keyvalues[0].Key) / length).
                ToArray();
        }

        private static (double key, double percent)[] BuildPercentsAlong4(KeyValuePair<double, double>[] keyvalues, BezierSegment3D_wpf[] bezier)
        {
            double total_len_keys = keyvalues[^1].Key - keyvalues[0].Key;

            // Find the closest distance between keys
            var key_distances = Enumerable.Range(0, keyvalues.Length - 1).
                Select(o => keyvalues[o + 1].Key - keyvalues[o].Key).       // the list is already sorted
                OrderBy(o => o).
                //Take(1).
                ToArray();

            // Get more samples than the keyvalues
            int count = Math.Min((total_len_keys / key_distances[0]) * 16, 144).ToInt_Ceiling();

            Point3D[] points_bezier = BezierUtil.GetPoints(count, bezier);

            double[] lengths_bezier = Enumerable.Range(0, points_bezier.Length - 1).
                Select(o => (points_bezier[o + 1] - points_bezier[o]).Length).
                ToArray();

            double total_len_bezier = lengths_bezier.Sum();

            var retVal = new List<(double key, double percent)>();

            retVal.Add((keyvalues[0].Key, 0));

            // walk the bezier points, getting the accumulated length, divide by total length
            double sum_length = 0;
            for (int i = 0; i < lengths_bezier.Length; i++)
            {
                sum_length += lengths_bezier[i];
                retVal.Add((points_bezier[i + 1].X, sum_length / total_len_bezier));
            }

            //retVal.Add((keyvalues[^1].Key, 1));

            return retVal.ToArray();
        }

        private static BezierSegment3D_wpf[] BuildBezier(KeyValuePair<double, double>[] keyvalues)
        {
            Point3D[] points = keyvalues.
                Select(o => new Point3D(o.Key, o.Value, 0)).
                ToArray();

            return BezierUtil.GetBezierSegments(points, 0.15);
        }
        // This is invalid, since the curve won't touch the interior keyvalue points
        private static BezierSegment3D_wpf[] BuildBezier_ALT(KeyValuePair<double, double>[] keyvalues)
        {
            Point3D end0 = new Point3D(keyvalues[0].Key, keyvalues[0].Value, 0);
            Point3D end1 = new Point3D(keyvalues[^1].Key, keyvalues[^1].Value, 0);

            Point3D[] control = keyvalues.
                Skip(1).
                Take(keyvalues.Length - 2).
                Select(o => new Point3D(o.Key, o.Value, 0)).
                ToArray();

            return new[] { new BezierSegment3D_wpf(end0, end1, control) };
        }
    }
}
