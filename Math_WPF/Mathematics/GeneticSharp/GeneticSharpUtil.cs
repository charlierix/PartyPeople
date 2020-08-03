using Game.Math_WPF.Mathematics;
using GeneticSharp.Domain.Chromosomes;
using GeneticSharp.Domain.Populations;
using GeneticSharp.Domain.Selections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Game.Math_WPF.Mathematics.GeneticSharp
{
    public static class GeneticSharpUtil
    {
        /// <summary>
        /// This helps determine how many bits to use for FloatingPointChromosome
        /// </summary>
        /// <remarks>
        /// Here is the code that converts to bits
        /// https://github.com/giacomelli/GeneticSharp/blob/720e95e81360a4e33e1c1711c584b8561e318a4c/src/GeneticSharp.Infrastructure.Framework/Commons/BinaryStringRepresentation.cs
        /// 
        /// ToRepresentation(double value, int totalBits = 0, int fractionDigits = 2)
        /// ToRepresentation(long value, int totalBits, bool throwsException)
        /// 
        /// The functions convert to a long, then a string of zeros and ones
        /// </remarks>
        public static int GetChromosomeBits(double maxValue, int fractionDigits)
        {
            // When it's negative, the conversion to bits uses all 64, most of the leftmost bits are one.  When the chromosome
            // picks random numbers, it converts the bits to a long, then clamps min to max.  Which means most arrangements of
            // bits would be way negative, and getting converted to min -- very inneficient
            if (maxValue <= 0)
                throw new ArgumentException($"maxValue must be positive: {maxValue}");

            if (fractionDigits < 0)
                throw new ArgumentException($"fractionDigits can't be negative: {fractionDigits}");

            long longValue = fractionDigits == 0 ?
                Convert.ToInt64(maxValue) :
                Convert.ToInt64(maxValue * Math.Pow(10, fractionDigits));       //TODO: See if this needs to be all 9s

            string bits = Convert.ToString(longValue, 2);

            return bits.Length;
        }

        // These translate values so that the chromosome uses zero as the min.  This makes the most efficient use of the bits
        public static double FromChromosome(double min, double value)
        {
            return value + min;
        }
        public static double ToChromosome(double min, double value)
        {
            return value - min;
        }

        /// <summary>
        /// This helps determine how many decimal places it would take to have a number of significant digits
        /// </summary>
        /// <remarks>
        /// This is used to tell FloatingPointChromosome how many decimal places to use
        /// 
        /// If you want 5 significant digits and the values passed in only use one integer position (0-9), you would
        /// need four decimal places.  If the integers are in the millions, then there would be no decimal places
        /// 
        /// The concept is copied from ToStringSignificantDigits_Standard
        /// </remarks>
        public static int GetNumDecimalPlaces(int desiredSignificantDigits, params double[] values)
        {
            double min = values.Min();
            double max = values.Max();

            double largest = max - min;

            var intPortion = new System.Numerics.BigInteger(Math.Truncate(largest));

            int numInt = intPortion == 0 ?
                0 :
                intPortion.ToString().Length;

            return Math.Max(desiredSignificantDigits - numInt, 0);
        }
    }

    #region class: ErrorSelection

    //WARNING: Even though this selects the ones with the lowest score, GeneticAlgorithm.BestChromosome is still getting set to the
    //highest value in that set (probably somewhere in the population code?)
    //
    //So until that gets figured out, this class is useless

    /// <summary>
    /// Selects the chromosomes with the lowest error
    /// </summary>
    /// <remarks>
    /// This is a copy of EliteSelection, just ordering low to high instead of high to low
    /// </remarks>    
    public sealed class ErrorSelection : SelectionBase
    {
        public ErrorSelection() :
            base(2)
        {
        }

        #region ISelection implementation

        /// <summary>
        /// Performs the selection of chromosomes from the generation specified.
        /// </summary>
        /// <param name="number">The number of chromosomes to select.</param>
        /// <param name="generation">The generation where the selection will be made.</param>
        /// <returns>The select chromosomes.</returns>
        protected override IList<IChromosome> PerformSelectChromosomes(int number, Generation generation)
        {
            return generation.Chromosomes.
                OrderBy(c => c.Fitness).       // the only change between this class and EliteSelection is the other uses OrderByDescending
                Take(number).
                ToList();
        }

        #endregion
    }

    #endregion
}
