using Game.Math_WPF.Mathematics;
using Game.Math_WPF.Mathematics.GeneticSharp;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
using Game.ML;
using GeneticSharp.Domain;
using GeneticSharp.Domain.Chromosomes;
using GeneticSharp.Domain.Crossovers;
using GeneticSharp.Domain.Fitnesses;
using GeneticSharp.Domain.Mutations;
using GeneticSharp.Domain.Populations;
using GeneticSharp.Domain.Selections;
using GeneticSharp.Domain.Terminations;
using System;
using System.Collections.Generic;
using System.Linq;
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
    /// https://github.com/giacomelli/GeneticSharp
    /// </summary>
    public partial class GeneticSharpTester : Window
    {
        #region Constructor

        public GeneticSharpTester()
        {
            InitializeComponent();

            Background = SystemColors.ControlBrush;
        }

        #endregion

        #region Event Listeners

        /// <summary>
        /// This is the example from this page:
        /// http://diegogiacomelli.com.br/function-optimization-with-geneticsharp/
        /// </summary>
        private void MaxEuclideanDistance_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                float maxWidth = 998f;
                float maxHeight = 680f;

                int bits = GeneticSharpUtil.GetChromosomeBits(Math.Max(maxWidth, maxHeight).ToInt_Ceiling(), 0);

                //NOTE: The arrays are length 4 because they are FromPoint and ToPoint
                var chromosome = new FloatingPointChromosome(
                    new double[] { 0, 0, 0, 0 },
                    new double[] { maxWidth, maxHeight, maxWidth, maxHeight },
                    new int[] { bits, bits, bits, bits },       // The total bits used to represent each number. The maximum value is 998, so 10 bits is what we need
                    new int[] { 0, 0, 0, 0 });      // The number of fraction (scale or decimal) part of the number. In our case we will not use any.  TODO: See if this means that only integers are considered

                var population = new Population(50, 100, chromosome);

                var fitness = new FuncFitness(c =>
                {
                    var fc = c as FloatingPointChromosome;

                    var values = fc.ToFloatingPoints();
                    var x1 = values[0];
                    var y1 = values[1];
                    var x2 = values[2];
                    var y2 = values[3];

                    return Math.Sqrt(((x2 - x1) * (x2 - x1)) + ((y2 - y1) * (y2 - y1)));
                });

                var selection = new EliteSelection();       // the larger the score, the better

                var crossover = new UniformCrossover(0.5f);     // .5 will pull half from each parent

                var mutation = new FlipBitMutation();       // FloatingPointChromosome inherits from BinaryChromosomeBase, which is a series of bits.  This mutator will flip random bits

                var termination = new FitnessStagnationTermination(144);        // keeps going until it generates the same winner this many generations in a row

                var ga = new GeneticAlgorithm(population, fitness, selection, crossover, mutation)
                {
                    Termination = termination,
                };

                txtLog.Text = "Starting...\r\n";

                double latestFitness = 0;
                var winners = new List<double[]>();

                ga.GenerationRan += (s1, e1) =>
                {
                    var bestChromosome = ga.BestChromosome as FloatingPointChromosome;
                    double bestFitness = bestChromosome.Fitness.Value;

                    if (bestFitness != latestFitness)
                    {
                        latestFitness = bestFitness;
                        double[] phenotype = bestChromosome.ToFloatingPoints();

                        txtLog.Text += string.Format(
                            "Generation {0,2}: ({1}, {2}) to ({3}, {4}) = {5}\r\n",
                            ga.GenerationsNumber,
                            phenotype[0],
                            phenotype[1],
                            phenotype[2],
                            phenotype[3],
                            Math.Round(bestFitness, 2));

                        winners.Add(phenotype);
                    }
                };

                ga.TerminationReached += (s2, e2) =>
                {
                    Debug3DWindow window = new Debug3DWindow();

                    var sizes = Debug3DWindow.GetDrawSizes(Math.Max(maxWidth, maxHeight) / 2d);

                    window.AddLines(new[] { new Point3D(), new Point3D(maxWidth, 0, 0), new Point3D(maxWidth, maxHeight, 0), new Point3D(0, maxHeight, 0), new Point3D() }, sizes.line, UtilityWPF.ColorFromHex("AAA"));

                    window.AddDots(winners.Select(o => new Point3D(o[0], o[1], 0)), sizes.line, Colors.Green);
                    window.AddDots(winners.Select(o => new Point3D(o[2], o[3], 0)), sizes.line, Colors.Red);

                    window.AddLines(winners.Select(o => (new Point3D(o[0], o[1], 0), new Point3D(o[2], o[3], 0))), sizes.line, UtilityWPF.ColorFromHex("AAA"));
                    window.AddLine(new Point3D(winners[^1][0], winners[^1][1], 0), new Point3D(winners[^1][2], winners[^1][3], 0), sizes.line, Colors.Black);

                    window.Show();
                };

                ga.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// This attempts to use IntegerChromosome, but it's returning int.Min (or close to it).  Just use the floating point
        /// chromosome with zero decimal places
        /// </summary>
        private void NegativeNumber_Click(object sender, RoutedEventArgs e)
        {
            const int MIN = -288;
            const int MAX = 288;
            const int FIND = 72;

            try
            {
                //var chromosome = new IntegerChromosome(MIN, MAX);     // IntegerChromosome seems flawed (just use floating point with 0 decimal places)
                var chromosome = new IntegerChromosome(MIN, MAX);

                var population = new Population(72, 144, chromosome);

                long maxError = MAX - MIN + 12;

                var fitness = new FuncFitness(c =>
                {
                    var ic = c as IntegerChromosome;
                    var value = ic.ToInteger();

                    return maxError - Math.Abs(value - FIND);       // need to return in a format where largest value wins
                });

                var selection = new EliteSelection();       // the larger the score, the better

                var crossover = new UniformCrossover(0.5f);     // .5 will pull half from each parent

                var mutation = new FlipBitMutation();       // FloatingPointChromosome inherits from BinaryChromosomeBase, which is a series of bits.  This mutator will flip random bits

                var termination = new FitnessStagnationTermination(144);        // keeps going until it generates the same winner this many generations in a row

                var ga = new GeneticAlgorithm(population, fitness, selection, crossover, mutation)
                {
                    Termination = termination,
                };

                txtLog.Text = "Starting...\r\n";

                double latestFitness = 0;
                var winners = new List<int>();

                ga.GenerationRan += (s1, e1) =>
                {
                    var bestChromosome = ga.BestChromosome as IntegerChromosome;
                    double bestFitness = bestChromosome.Fitness.Value;

                    if (bestFitness != latestFitness)
                    {
                        latestFitness = bestFitness;
                        int phenotype = bestChromosome.ToInteger();

                        txtLog.Text += string.Format(
                            "Generation {0,2}: {1} = {2}\r\n",
                            ga.GenerationsNumber,
                            phenotype,
                            Math.Round(bestFitness, 2));

                        winners.Add(phenotype);
                    }
                };

                ga.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        /// <remarks>
        /// https://github.com/giacomelli/GeneticSharp/blob/69ad1ebb3ac5c3e9de9ad65456dc30a7abbd6828/src/GeneticSharp.Domain/Chromosomes/FloatingPointChromosome.cs
        /// 
        /// FloatingPointChromosome clamps values that are outside the min max range to min and max.  But negative needs
        /// 64 bits (most of the left ones are 1s).  So while it works, it's inneficient
        /// 
        /// So instead of negative numbers, just shift so that zero is the min number
        /// </remarks>
        private void NegativeNumber_Shifted_Click(object sender, RoutedEventArgs e)
        {
            const long MIN = -288;
            const long MAX = 288;
            const long FIND = 72;

            try
            {
                int bits = GeneticSharpUtil.GetChromosomeBits(GeneticSharpUtil.ToChromosome(MIN, MAX), 0);

                var chromosome = new FloatingPointChromosome(
                    new double[] { 0 },
                    new double[] { GeneticSharpUtil.ToChromosome(MIN, MAX) },
                    new int[] { bits },       // The total bits used to represent each number
                    new int[] { 0 });      // The number of fraction (scale or decimal) part of the number. In our case we will not use any.  TODO: See if this means that only integers are considered

                var population = new Population(72, 144, chromosome);

                long maxError = MAX - MIN + 12;

                var fitness = new FuncFitness(c =>
                {
                    var fc = c as FloatingPointChromosome;
                    var values = fc.ToFloatingPoints();

                    return maxError - Math.Abs(values[0] - GeneticSharpUtil.ToChromosome(MIN, FIND));       // need to return in a format where largest value wins
                });

                var selection = new EliteSelection();       // the larger the score, the better

                var crossover = new UniformCrossover(0.5f);     // .5 will pull half from each parent

                var mutation = new FlipBitMutation();       // FloatingPointChromosome inherits from BinaryChromosomeBase, which is a series of bits.  This mutator will flip random bits

                var termination = new FitnessStagnationTermination(144);        // keeps going until it generates the same winner this many generations in a row

                var ga = new GeneticAlgorithm(population, fitness, selection, crossover, mutation)
                {
                    Termination = termination,
                };

                txtLog.Text = "Starting...\r\n";

                double latestFitness = 0;
                var winners = new List<double[]>();

                ga.GenerationRan += (s1, e1) =>
                {
                    var bestChromosome = ga.BestChromosome as FloatingPointChromosome;
                    double bestFitness = bestChromosome.Fitness.Value;

                    if (bestFitness != latestFitness)
                    {
                        latestFitness = bestFitness;
                        double[] phenotype = bestChromosome.ToFloatingPoints();

                        txtLog.Text += string.Format(
                            "Generation {0,2}: {1} = {2}\r\n",
                            ga.GenerationsNumber,
                            GeneticSharpUtil.FromChromosome(MIN, phenotype[0]),      // showing the value in world coords
                            Math.Round(bestFitness, 2));

                        winners.Add(phenotype);
                    }
                };

                ga.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void FloatingPoint_Click(object sender, RoutedEventArgs e)
        {
            const double MIN = 256;
            const double MAX = 1024;
            const double FIND = 700.007;

            try
            {
                int bits = GeneticSharpUtil.GetChromosomeBits(MAX, 3);

                var chromosome = new FloatingPointChromosome(
                    new double[] { MIN },
                    new double[] { MAX },
                    new int[] { bits },       // The total bits used to represent each number
                    new int[] { 3 });      // The number of fraction (scale or decimal) part of the number. In our case we will not use any.  TODO: See if this means that only integers are considered

                var population = new Population(72, 144, chromosome);

                double maxError = MAX - MIN + 12;

                var fitness = new FuncFitness(c =>
                {
                    var fc = c as FloatingPointChromosome;
                    var values = fc.ToFloatingPoints();

                    return maxError - Math.Abs(values[0] - FIND);       // need to return in a format where largest value wins
                });

                var selection = new EliteSelection();       // the larger the score, the better

                var crossover = new UniformCrossover(0.5f);     // .5 will pull half from each parent

                var mutation = new FlipBitMutation();       // FloatingPointChromosome inherits from BinaryChromosomeBase, which is a series of bits.  This mutator will flip random bits

                var termination = new FitnessStagnationTermination(144);        // keeps going until it generates the same winner this many generations in a row

                var ga = new GeneticAlgorithm(population, fitness, selection, crossover, mutation)
                {
                    Termination = termination,
                };

                txtLog.Text = "Starting...\r\n";

                double latestFitness = 0;
                var winners = new List<double[]>();

                ga.GenerationRan += (s1, e1) =>
                {
                    var bestChromosome = ga.BestChromosome as FloatingPointChromosome;
                    double bestFitness = bestChromosome.Fitness.Value;

                    if (bestFitness != latestFitness)
                    {
                        latestFitness = bestFitness;
                        double[] phenotype = bestChromosome.ToFloatingPoints();

                        txtLog.Text += string.Format(
                            "Generation {0,2}: {1} = {2}\r\n",
                            ga.GenerationsNumber,
                            phenotype[0],
                            Math.Round(bestFitness, 2));

                        winners.Add(phenotype);
                    }
                };

                ga.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FloatingPointChromosome2_Click(object sender, RoutedEventArgs e)
        {
            //const double MIN = -12;
            //const double MAX = 12;
            //const double FIND = 7.777;

            //const double MIN = 512;
            //const double MAX = 1050;
            //const double FIND = 700.007;

            const double MIN = -36;
            const double MAX = -24;
            const double FIND = -30;

            try
            {
                var chromosome = FloatingPointChromosome2.Create(
                    new double[] { MIN },
                    new double[] { MAX },
                    new int[] { 2 });

                var population = new Population(72, 144, chromosome);

                double maxError = MAX - MIN + 12;

                var log = new List<double>();

                var fitness = new FuncFitness(c =>
                {
                    var fc = c as FloatingPointChromosome2;
                    var values = fc.ToFloatingPoints();

                    log.Add(values[0]);

                    return maxError - Math.Abs(values[0] - FIND);       // need to return in a format where largest value wins
                });

                var selection = new EliteSelection();       // the larger the score, the better

                var crossover = new UniformCrossover(0.5f);     // .5 will pull half from each parent

                var mutation = new FlipBitMutation();       // FloatingPointChromosome inherits from BinaryChromosomeBase, which is a series of bits.  This mutator will flip random bits

                var termination = new FitnessStagnationTermination(144);        // keeps going until it generates the same winner this many generations in a row

                var ga = new GeneticAlgorithm(population, fitness, selection, crossover, mutation)
                {
                    Termination = termination,
                };

                txtLog.Text = "Starting...\r\n";

                double latestFitness = 0;
                var winners = new List<double[]>();

                ga.GenerationRan += (s1, e1) =>
                {
                    var bestChromosome = ga.BestChromosome as FloatingPointChromosome2;
                    double bestFitness = bestChromosome.Fitness.Value;

                    if (bestFitness != latestFitness)
                    {
                        latestFitness = bestFitness;
                        double[] phenotype = bestChromosome.ToFloatingPoints();

                        txtLog.Text += string.Format(
                            "Generation {0,2}: {1} = {2}\r\n",
                            ga.GenerationsNumber,
                            phenotype[0],
                            Math.Round(bestFitness, 2));

                        winners.Add(phenotype);
                    }
                };

                ga.TerminationReached += (s1, e1) =>
                {
                    var outOfRange = log.
                        Where(o => o < MIN || o > MAX).
                        ToArray();

                    if (outOfRange.Length > 0)
                        txtLog.Text += "----- OUT OF RANGE ----\r\n";
                };

                ga.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
