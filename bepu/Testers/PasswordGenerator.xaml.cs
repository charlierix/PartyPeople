using Game.Core;
using GameItems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;

namespace Game.Bepu.Testers
{
    public partial class PasswordGenerator : Window
    {
        private record AvailableCharacters
        {
            public char[] All { get; init; }
            public char[] Lower { get; init; }
            public char[] Upper { get; init; }
            public char[] Number { get; init; }
            public char[] Special { get; init; }
        }

        public PasswordGenerator()
        {
            InitializeComponent();

            Background = SystemColors.ControlBrush;
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!int.TryParse(txtNumChars.Text, out int num_chars))
                {
                    MessageBox.Show("Couldn't parse number of characters as integer", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                else if (num_chars < 8)     // need a few to account for numbers, upper, special.  Less than 8 is just bad anyway
                {
                    MessageBox.Show("Number of characters must be at least 8", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var characters = GetAvailableChars(txtSpecialChars.Text);

                txtResult.Text = GetPassword_MinOneOfEach(characters, num_chars);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static AvailableCharacters GetAvailableChars(string special_chars)
        {
            // This should work, but seems more complex than necessary
            //byte a = ASCIIEncoding.ASCII.GetBytes("a")[0];

            //char[] lower = Enumerable.Range(0, 26).
            //    Select(o => ASCIIEncoding.ASCII.GetString(new[] { Convert.ToByte(a + o) })[0]).
            //    ToArray();

            char[] lower = new[] { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z' };

            char[] upper = lower.
                Select(o => o.ToString().ToUpper()[0]).
                ToArray();

            char[] number = new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };

            char[] special = (special_chars ?? "").
                Where(o => !string.IsNullOrWhiteSpace(o.ToString())).
                Distinct().
                ToArray();

            return new AvailableCharacters()
            {
                All = lower.
                    Concat(upper).
                    Concat(number).
                    Concat(special).
                    ToArray(),

                Lower = lower,
                Upper = upper,
                Number = number,
                Special = special,
            };
        }

        // This generates a string from all available chars (but some categories might get skipped)
        private static string GetPassword_NoConstraints(AvailableCharacters characters, int num_chars)
        {
            int max = characters.All.Length;

            Random rand = StaticRandom.GetRandomForThread();

            char[] password = Enumerable.Range(0, num_chars).
                Select(o => characters.All[rand.Next(max)]).
                ToArray();

            return new string(password);
        }

        // This makes sure the final string contains at least one character from each set
        private static string GetPassword_MinOneOfEach(AvailableCharacters characters, int num_chars)
        {
            // Lay out sets and counts
            var char_sets = new List<char[]>()
            {
                characters.Lower,
                characters.Upper,
                characters.Number,
            };

            if (characters.Special.Length > 0)
                char_sets.Add(characters.Special);

            int[] charset_counts = char_sets.
                Select(o => o.Length).
                ToArray();

            // Figure out how many to use from each set (there will be a min of one from each)
            int[] counts = GetPassword_Counts(charset_counts, num_chars);

            // Generate random characters from each character set
            char[][] actual_jagged = GetPassword_Actual_Jagged(char_sets.ToArray(), counts);

            // Flatten into 1D array
            var actual_list = actual_jagged.
                SelectMany(o => o).
                ToArray();

            // Walk the list in a random order
            return new string(UtilityCore.RandomOrder(actual_list).ToArray());
        }
        private static int[] GetPassword_Counts(int[] category_counts, int num_chars)
        {
            int num_categories = category_counts.Length;

            if (category_counts.Any(o => o < 1))
                throw new ArgumentOutOfRangeException($"There must be at least one in each catetory: {category_counts.Select(o => o.ToString()).ToJoin(", ")}");

            if (num_categories > num_chars)
                throw new ArgumentOutOfRangeException($"There are more categories than characters.  categories: {num_categories}, chars: {num_chars}");

            // The values in pool are index into category, but the counts allow proper ratios
            var pool = Enumerable.Range(0, category_counts.Length).
                SelectMany(o => Enumerable.Range(0, category_counts[o]).
                    Select(p => o).
                    ToArray()).
                ToArray();

            // Make sure there are at least one of each
            int[] retVal = Enumerable.Range(0, num_categories).
                Select(o => 1).
                ToArray();

            Random random = StaticRandom.GetRandomForThread();

            for (int i = num_categories + 1; i <= num_chars; i++)
            {
                int index = random.Next(0, pool.Length);
                retVal[pool[index]]++;
            }

            return retVal;
        }
        private static char[][] GetPassword_Actual_Jagged(char[][] chars, int[] counts)
        {
            // First attempt was a nested linq statement, but it was abstract and fiddly.  Blowing out into a function to make the
            // logic clearer

            Random rand = StaticRandom.GetRandomForThread();

            var retVal = new List<char[]>();

            for (int i = 0; i < chars.Length; i++)
            {
                char[] actual = Enumerable.Range(0, counts[i]).
                    Select(o =>
                    {
                        int index = rand.Next(chars[i].Length);
                        return chars[i][index];
                    }).
                    ToArray();

                retVal.Add(actual);
            }

            return retVal.ToArray();
        }
    }
}
