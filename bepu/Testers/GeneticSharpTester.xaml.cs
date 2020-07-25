using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
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

                int bits = GetChromosomeBits(Math.Max(maxWidth, maxHeight).ToInt_Ceiling());

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

                var latestFitness = 0.0;
                var winners = new List<double[]>();

                ga.GenerationRan += (s1, e1) =>
                {
                    var bestChromosome = ga.BestChromosome as FloatingPointChromosome;
                    var bestFitness = bestChromosome.Fitness.Value;

                    if (bestFitness != latestFitness)
                    {
                        latestFitness = bestFitness;
                        var phenotype = bestChromosome.ToFloatingPoints();

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

        #endregion

        #region Private Methods

        //TODO: Make a version that can handle floating point values
        private static int GetChromosomeBits(int maxValue)
        {
            //https://www.calculatorsoup.com/calculators/algebra/exponentsolve.php

            //TODO: Figure out how to handle negative numbers - is abs enough? (test with genetic sharp to see if it can handle them).  May need to just shift to positive values
            if (maxValue <= 0)
                throw new ArgumentException("maxValue must be positive");

            return (Math.Log(maxValue) / Math.Log(2)).ToInt_Ceiling();
        }

        #endregion
    }
}
