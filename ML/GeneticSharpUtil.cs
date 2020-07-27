using Game.Math_WPF.Mathematics;
using GeneticSharp.Domain.Chromosomes;
using GeneticSharp.Domain.Populations;
using GeneticSharp.Domain.Selections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Game.ML
{
    public static class GeneticSharpUtil
    {
        //TODO: Make a version that can handle floating point values
        public static int GetChromosomeBits(int maxValue)
        {
            //https://www.calculatorsoup.com/calculators/algebra/exponentsolve.php

            //TODO: Figure out how to handle negative numbers - is abs enough? (test with genetic sharp to see if it can handle them).  May need to just shift to positive values
            if (maxValue <= 0)
                throw new ArgumentException("maxValue must be positive");

            return (Math.Log(maxValue) / Math.Log(2)).ToInt_Ceiling();
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
