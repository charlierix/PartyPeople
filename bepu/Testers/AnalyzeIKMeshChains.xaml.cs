using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
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
    public partial class AnalyzeIKMeshChains : Window
    {
        #region class: SerializeScene

        [Serializable]
        private class SerializeScene
        {
            // "name|x|y|z"
            public string[] Joints { get; set; }

            // "name1|name2"
            public string[] Links { get; set; }

            public SerializeChain[] Chains { get; set; }
        }

        #endregion
        #region class: SerializeChain

        [Serializable]
        public class SerializeChain
        {
            // The name of the target that this chain is tied to
            public string TargetName { get; set; }

            // Joints going from target to root
            public string[] JointNames { get; set; }
        }

        #endregion

        #region class: JointVisual

        private class JointVisual
        {
            public string ID { get; set; }
            public long Token { get; set; }

            public Point3D Position { get; set; }

            public Visual3D Visual { get; set; }
            public DiffuseMaterial Material { get; set; }

            public JointVisual[] Links { get; set; }
        }

        #endregion
        #region class: ChainVisual

        private class ChainVisual
        {
            public long Token { get; } = TokenGenerator.NextToken();

            public string TargetName { get; set; }
            public long TargetToken { get; set; }

            public string[] JointIDs { get; set; }
            public long[] JointTokens { get; set; }
            public Point3D[] JointPoints { get; set; }
            public JointVisual[] Joints { get; set; }

            public Visual3D Visual { get; set; }
            public DiffuseMaterial Diffuse { get; set; }
            public EmissiveMaterial Emissive { get; set; }
            public TranslateTransform3D Transform { get; set; }

            public bool EndsInLoop { get; set; }
        }

        #endregion

        #region Declaration Section

        private const string BASECOLOR = "999";

        private TrackBallRoam _trackball = null;

        private List<JointVisual> _joints = new List<JointVisual>();
        private List<ChainVisual> _chains = new List<ChainVisual>();

        private Dictionary<string, long> _tokens_joint = new Dictionary<string, long>();
        private Dictionary<string, long> _tokens_target = new Dictionary<string, long>();

        #endregion

        #region Constructor

        public AnalyzeIKMeshChains()
        {
            InitializeComponent();

            // Trackball
            _trackball = new TrackBallRoam(_camera);
            _trackball.EventSource = grdViewPort;       //NOTE:  If this control doesn't have a background color set, the trackball won't see events (I think transparent is ok, just not null)
            _trackball.AllowZoomOnMouseWheel = true;
            _trackball.Mappings.AddRange(TrackBallMapping.GetPrebuilt(TrackBallMapping.PrebuiltMapping.MouseComplete_NoLeft));
            _trackball.ShouldHitTestOnOrbit = false;
            _trackball.MouseWheelScale *= .3;
        }

        #endregion

        #region Event Listeners

        private void Load_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string filename = PromptForFilename();
                if (string.IsNullOrWhiteSpace(filename))
                    return;

                ClearScene();

                lblFilename.Content = System.IO.Path.GetFileName(filename);

                var deserialized = DeserializeScene(filename);

                // Convert deserialized to objects (and add visuals)
                AddJoints(deserialized.Joints);
                AddChains(deserialized.Chains);
                AddTargets(deserialized.Chains);

                // Do post processing to fill in some properties
                PopulateJointLinks(_joints, _chains);
                PopulateJoints(_chains, _joints);
                PopulateEndsInLoop(_chains);

                StringBuilder report = new StringBuilder();

                report.AppendLine($"Targets: {panelTargets.Children.Count}");
                report.AppendLine($"Joints: {_joints.Count}");
                report.AppendLine($"Chains: {_chains.Count}");

                lblCounts.Content = report.ToString();


                // have a checkbox that tells the links to draw on the same plane, or a unique for each


                //--- if they right click a joint or target, show a context menu
                //    spawn debug windows that show more details

                // Draw all the joints.  Store each in a class that holds the video and can respond to mouse events
                //    When the mouse clicks a joint, highlight all paths that touch that joint

                // Draw all the lines.  Store each in a similar class

                // Draw the targets near their corresponding joint
                //    When the mouse clicks a target, highlight all paths that originate from that target


            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void chkLoopEnds_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_chains == null)
                    return;

                ResetLines();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Target_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_chains == null)
                    return;

                ResetLines();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void grdViewPort_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ChangedButton != MouseButton.Left || _joints.Count == 0)
                    return;

                var hits = UtilityWPF.CastRay(out _, e.GetPosition(grdViewPort), grdViewPort, _camera, _viewport, true);

                var jointHit = _joints.
                    FirstOrDefault(o => hits.Any(p => p.ModelHit.VisualHit == o.Visual));

                if (jointHit != null)
                {
                    HighlightLines(new[] { jointHit.Token });
                    return;
                }

                var chainHit = _chains.
                    FirstOrDefault(o => hits.Any(p => p.ModelHit.VisualHit == o.Visual));

                if (chainHit != null && chainHit.EndsInLoop)
                {
                    AnalyzeEndsInLoop(chainHit);
                    return;
                }

                // They clicked the background, reset lines
                ResetLines();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Private Method

        private void ClearScene()
        {
            lblFilename.Content = "";
            panelTargets.Children.Clear();

            _viewport.Children.RemoveAll(_joints.Select(o => o.Visual));
            _joints.Clear();

            _viewport.Children.RemoveAll(_chains.Select(o => o.Visual));
            _chains.Clear();

            _tokens_joint.Clear();
        }

        private void AddJoints(string[] joints)
        {
            foreach (string[] jointArr in joints.Select(o => o.Split('|')))
            {
                JointVisual item = new JointVisual()
                {
                    ID = jointArr[0],
                    Token = GetToken(jointArr[0], _tokens_joint),
                    Position = new Point3D(double.Parse(jointArr[1]), double.Parse(jointArr[2]), double.Parse(jointArr[3])),

                    Material = new DiffuseMaterial(),

                    //NOTE: .Links is populated in a post process function
                };

                MaterialGroup material = new MaterialGroup();
                material.Children.Add(item.Material);
                material.Children.Add(new SpecularMaterial(UtilityWPF.BrushFromHex("6CCC"), 3));

                GeometryModel3D model = new GeometryModel3D();
                model.Material = material;
                model.BackMaterial = material;

                model.Geometry = UtilityWPF.GetSphere_Ico(.3, 1, true);

                model.Transform = new TranslateTransform3D(item.Position.ToVector());

                item.Visual = new ModelVisual3D()
                {
                    Content = model,
                };

                _viewport.Children.Add(item.Visual);

                _joints.Add(item);
            }
        }
        private void AddChains(SerializeChain[] chains)
        {
            foreach (var chain in chains)
            {
                ChainVisual item = new ChainVisual()
                {
                    TargetName = chain.TargetName,
                    TargetToken = GetToken(chain.TargetName, _tokens_target),

                    JointIDs = chain.JointNames,
                    JointTokens = chain.JointNames.
                        Select(o => GetToken(o, _tokens_joint)).
                        ToArray(),

                    Transform = new TranslateTransform3D(0, 0, 0),
                };

                item.JointPoints = item.JointTokens.
                    Select(o => _joints.First(p => p.Token == o).Position).
                    ToArray();

                var bezierSegments = BezierUtil.GetBezierSegments(item.JointPoints);

                Point3D[] bezierPoints = BezierUtil.GetPoints(36, bezierSegments);

                var material = UtilityWPF.GetUnlitMaterial_Components(UtilityWPF.ColorFromHex(BASECOLOR));
                item.Diffuse = material.diffuse;
                item.Emissive = material.emissive;

                Model3DGroup group = new Model3DGroup();

                for (int cntr = 0; cntr < bezierPoints.Length - 1; cntr++)
                {
                    GeometryModel3D model = new GeometryModel3D();
                    model.Material = material.final;
                    model.BackMaterial = material.final;

                    model.Geometry = UtilityWPF.GetLine(bezierPoints[cntr], bezierPoints[cntr + 1], .06);

                    group.Children.Add(model);
                }

                group.Transform = item.Transform;

                item.Visual = new ModelVisual3D()
                {
                    Content = group,
                };

                _viewport.Children.Add(item.Visual);

                _chains.Add(item);
            }
        }
        private void AddTargets(SerializeChain[] chains)
        {
            var unique = chains.
                Select(o => new
                {
                    name = o.TargetName,
                    token = GetToken(o.TargetName, _tokens_target),
                }).
                ToLookup(o => o.token).
                Select(o => new CheckBox()
                {
                    Content = o.First().name,
                    IsChecked = true,
                    Tag = o.Key,
                }).
                ToArray();

            foreach (var checkbox in unique)
            {
                panelTargets.Children.Add(checkbox);
            }
        }

        private static void PopulateJointLinks(List<JointVisual> joints, List<ChainVisual> chains)
        {
            foreach (var joint in joints)
            {
                var touching = new List<long>();

                foreach (var chain in chains)
                {
                    for (int cntr = 0; cntr < chain.JointTokens.Length; cntr++)
                    {
                        if (chain.JointTokens[cntr] == joint.Token)
                        {
                            if (cntr > 0)
                                touching.Add(chain.JointTokens[cntr - 1]);

                            if (cntr < chain.JointTokens.Length - 1)
                                touching.Add(chain.JointTokens[cntr + 1]);

                            break;
                        }
                    }
                }

                joint.Links = touching.
                    Distinct().
                    OrderBy().
                    Select(o => joints.First(p => p.Token == o)).
                    ToArray();
            }
        }
        private void PopulateJoints(List<ChainVisual> chains, List<JointVisual> joints)
        {
            foreach (var chain in chains)
            {
                chain.Joints = chain.JointTokens.
                    Select(o => joints.First(p => p.Token == o)).
                    ToArray();
            }
        }
        private static void PopulateEndsInLoop(List<ChainVisual> chains)
        {
            foreach (var chain in chains)
            {
                chain.EndsInLoop = chain.Joints[^1].Links.Length > 1;
            }
        }

        private void AnalyzeEndsInLoop(ChainVisual clickedChain)
        {
            const double ZSTEP = 1.2;

            if (!clickedChain.EndsInLoop)
            {
                ResetLines();
                return;
            }

            var links = Enumerable.Range(0, clickedChain.JointTokens.Length - 1).
                Select(o => (clickedChain.JointTokens[o], clickedChain.JointTokens[o + 1])).
                ToList();

            var discards = new List<ChainVisual>();

            var loops = new List<ChainVisual>();
            var nonLoops = new List<ChainVisual>();

            // Separate chains
            foreach (var chain in _chains)
            {
                if (chain.Token == clickedChain.Token)
                    continue;
                else if (chain.TargetToken != clickedChain.TargetToken)
                    discards.Add(chain);
                else if (chain.EndsInLoop)
                    loops.Add(chain);
                else
                    nonLoops.Add(chain);
            }

            var keep = new List<ChainVisual>();

            // First Pass: consider chains the don't end in loops
            AnalyzeEndsInLoop_Filter(nonLoops, keep, links);

            // Second Pass: consider chains that end in loops
            if (links.Count > 0)
                AnalyzeEndsInLoop_Filter(loops, keep, links);

            discards.AddRange(loops);
            discards.AddRange(nonLoops);

            if (discards.Count > 0)
                ColorAndMoveLines(discards, UtilityWPF.ColorFromHex(BASECOLOR), -.06, 0, false);

            Color color = links.Count == 0 && keep.Count > 0 ?
                Colors.Chartreuse :
                Colors.Red;

            ColorAndMoveLines(new[] { clickedChain }, color, ZSTEP, 0, false);

            if (keep.Count > 0)
            {
                Color[] colors = UtilityWPF.GetRandomColors(keep.Count, 100, 200);
                ColorAndMoveLines(keep, colors, ZSTEP * 2, ZSTEP, false);
            }
        }
        private static void AnalyzeEndsInLoop_Filter(List<ChainVisual> candidates, List<ChainVisual> keep, List<(long, long)> remainingLinks)
        {
            int index = 0;
            while (remainingLinks.Count > 0 && index < candidates.Count)
            {
                var links = Enumerable.Range(0, candidates[index].JointTokens.Length - 1).
                    Select(o => (candidates[index].JointTokens[o], candidates[index].JointTokens[o + 1])).
                    ToArray();

                var removed = remainingLinks.RemoveWhere(o => links.Any(p => (o.Item1 == p.Item1 && o.Item2 == p.Item2) || (o.Item1 == p.Item2 && o.Item2 == p.Item1)));

                if (removed.Count() > 0)
                {
                    keep.Add(candidates[index]);
                    candidates.RemoveAt(index);
                }
                else
                {
                    index++;
                }
            }
        }

        private void HighlightLines(long[] jointTokens)
        {
            const double ZSTEP = .6;

            // Find lines that touch the joints
            var touching = new List<ChainVisual>();
            var untouched = new List<ChainVisual>();

            foreach (var chain in _chains)
            {
                if (chain.JointTokens.Any(o => jointTokens.Contains(o)))
                    touching.Add(chain);
                else
                    untouched.Add(chain);
            }

            if (touching.Count == 0)
            {
                ResetLines();
                return;
            }

            // Untouched
            Color baseColor = UtilityWPF.ColorFromHex(BASECOLOR);

            ColorAndMoveLines(untouched, baseColor, -ZSTEP, -ZSTEP);

            // Touching
            var colors = UtilityWPF.GetRandomColors(touching.Count, 100, 200);

            ColorAndMoveLines(touching, colors, ZSTEP, ZSTEP);
        }
        private void ResetLines()
        {
            Color baseColor = UtilityWPF.ColorFromHex(BASECOLOR);

            ColorAndMoveLines(_chains, baseColor, 0, 0);
        }

        private void ColorAndMoveLines(IList<ChainVisual> chains, Color color, double initialZ, double zStep, bool considerLoopendsCheckbox = true)
        {
            Color[] colors = Enumerable.Range(0, chains.Count).
                Select(o => color).
                ToArray();

            ColorAndMoveLines(chains, colors, initialZ, zStep, considerLoopendsCheckbox);
        }
        private void ColorAndMoveLines(IList<ChainVisual> chains, Color[] colors, double initialZ, double zStep, bool considerLoopendsCheckbox = true)
        {
            bool markLoopEnds = considerLoopendsCheckbox && chkLoopEnds.IsChecked.Value;

            long[] targets = GetActiveTargets();

            double z = initialZ;

            for (int cntr = 0; cntr < chains.Count; cntr++)
            {
                if (!targets.Contains(chains[cntr].TargetToken))
                {
                    HideChain(chains[cntr]);
                    continue;
                }

                Color color = markLoopEnds && chains[cntr].EndsInLoop && z > 0 ?
                    Colors.Gray :
                    colors[cntr];

                UtilityWPF.UpdateUnlitColor(color, chains[cntr].Diffuse, chains[cntr].Emissive);

                chains[cntr].Transform.OffsetX = 0;
                chains[cntr].Transform.OffsetY = 0;
                chains[cntr].Transform.OffsetZ = z;

                z += zStep;
            }
        }

        private long[] GetActiveTargets()
        {
            return panelTargets.Children.
                AsEnumerabIe().
                Select(o => o as CheckBox).
                Where(o => o != null && o.IsChecked.Value).
                Select(o => (long)o.Tag).
                ToArray();
        }

        private static void HideChain(ChainVisual chain)
        {
            UtilityWPF.UpdateUnlitColor(Colors.Transparent, chain.Diffuse, chain.Emissive);

            chain.Transform.OffsetX = 0;
            chain.Transform.OffsetY = 0;
            chain.Transform.OffsetZ = -144;
        }

        private static long GetToken(string id, Dictionary<string, long> dictionary)
        {
            if (dictionary.TryGetValue(id.ToUpper(), out long retVal))
                return retVal;

            retVal = TokenGenerator.NextToken();

            dictionary.Add(id.ToUpper(), retVal);

            return retVal;
        }

        private static string PromptForFilename()
        {
            string folder = System.IO.Path.Combine(UtilityCore.GetOptionsFolder(), "IKMesh");

            // Prompt for file
            var dialog = new Microsoft.Win32.OpenFileDialog()
            {
                InitialDirectory = folder,
                //Filter = "*.xaml|*.xml|*.*",
                Multiselect = false,
            };

            bool? result = dialog.ShowDialog();
            if (result == null || result.Value == false)
            {
                return null;
            }

            return dialog.FileName;
        }

        private static SerializeScene DeserializeScene(string filename)
        {
            string jsonString = System.IO.File.ReadAllText(filename);

            return JsonSerializer.Deserialize<SerializeScene>(jsonString);
        }

        #endregion
    }
}
