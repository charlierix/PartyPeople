using GeneticSharp.Domain.Chromosomes;
using GeneticSharp.Domain.Randomizations;
using GeneticSharp.Infrastructure.Framework.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Game.ML
{
    /// <summary>
    /// Floating point chromosome with binary values (0 and 1).
    /// </summary>
    /// <remarks>
    /// Copied from this
    /// https://github.com/giacomelli/GeneticSharp/blob/69ad1ebb3ac5c3e9de9ad65456dc30a7abbd6828/src/GeneticSharp.Domain/Chromosomes/FloatingPointChromosome.cs
    /// 
    /// The original asks the user for number of bits.  This calculates the number for the user
    /// 
    /// Also, negative values are handled inneficiently, so this translates so min is always zero.  So internally, this stores
    /// values between 0 and N, but ToFloatingPoints() transforms into what the user expects
    /// </remarks>
    public class FloatingPointChromosome2 : BinaryChromosomeBase
    {
        private readonly double[] _minValue;
        private readonly double[] _maxValue;
        private readonly int[] _totalBits;
        private readonly int[] _fractionDigits;
        private readonly string _originalValueStringRepresentation;

        #region Constructor

        public static FloatingPointChromosome2 Create(double[] minValue, double[] maxValue, int[] fractionDigits, double[] geneValues = null)
        {
            int[] bits = Enumerable.Range(0, minValue.Length).
                Select(o => GeneticSharpUtil.GetChromosomeBits(GeneticSharpUtil.ToChromosome(minValue[o], maxValue[o]), fractionDigits[o])).
                ToArray();

            return new FloatingPointChromosome2(minValue, maxValue, fractionDigits, bits, geneValues);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:GeneticSharp.Domain.Chromosomes.FloatingPointChromosome"/> class.
        /// </summary>
        /// <param name="minValue">Minimum value.</param>
        /// <param name="maxValue">Max value.</param>
        /// <param name="totalBits">Total bits.</param>
        /// <param name="fractionDigits">Fraction digits.</param>
        /// /// <param name="geneValues">Gene values.</param>
        private FloatingPointChromosome2(double[] minValue, double[] maxValue, int[] fractionDigits, int[] totalBits, double[] geneValues = null)
            : base(totalBits.Sum())
        {
            _minValue = minValue;
            _maxValue = maxValue;
            _totalBits = totalBits;
            _fractionDigits = fractionDigits;

            // If values are not supplied, create random values
            if (geneValues == null)
            {
                geneValues = new double[minValue.Length];
                var rnd = RandomizationProvider.Current;

                for (int i = 0; i < geneValues.Length; i++)
                {
                    geneValues[i] = rnd.GetDouble(0, GeneticSharpUtil.ToChromosome(minValue[i], maxValue[i]));
                }
            }

            _originalValueStringRepresentation = String.Join(
                "",
                BinaryStringRepresentation.ToRepresentation(
                    geneValues,
                    totalBits,
                    fractionDigits));

            CreateGenes();
        }

        #endregion

        /// <summary>
        /// Creates the new.
        /// </summary>
        /// <returns>The new.</returns>
        public override IChromosome CreateNew()
        {
            return new FloatingPointChromosome2(_minValue, _maxValue, _fractionDigits, _totalBits);
        }

        /// <summary>
        /// Generates the gene.
        /// </summary>
        /// <returns>The gene.</returns>
        /// <param name="geneIndex">Gene index.</param>
        public override Gene GenerateGene(int geneIndex)
        {
            //NOTE: Each gene is just one bit, so converting to int32 is fine (index is the index into the total set of bits, not one of the floating points)
            return new Gene(Convert.ToInt32(_originalValueStringRepresentation[geneIndex].ToString()));
        }

        /// <summary>
        /// Converts the chromosome to the floating points representation.
        /// </summary>
        /// <returns>The floating points.</returns>
        public double[] ToFloatingPoints()
        {
            return BinaryStringRepresentation.ToDouble(ToString(), _totalBits, _fractionDigits).
                Select((o, i) =>
                {
                    double r = GeneticSharpUtil.FromChromosome(_minValue[i], o);
                    r = Math.Round(r, _fractionDigits[i]);      // because of the floating point arithmatic, something like 7.5 may end up 7.5000000000001, which wouldn't really cause errors but would be annoying when printing out results.  So rounding to force the desired digits
                    r = EnsureMinMax(r, i);
                    return r;
                }).
                ToArray();
        }

        private double EnsureMinMax(double value, int index)
        {
            return Math.Clamp(value, _minValue[index], _maxValue[index]);
        }

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:GeneticSharp.Domain.Chromosomes.FloatingPointChromosome"/>.
        /// </summary>
        /// <returns>A <see cref="T:System.String"/> that represents the current <see cref="T:GeneticSharp.Domain.Chromosomes.FloatingPointChromosome"/>.</returns>
        public override string ToString()
        {
            return String.Join("", GetGenes().Select(g => g.Value.ToString()).ToArray());
        }
    }
}
