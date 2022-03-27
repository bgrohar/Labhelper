using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Runtime.InteropServices;

namespace Labhelper
{
    public partial class MainWindow : Window
    {
        [DllImport("Kernel32")]
        static extern void AllocConsole();


        private int emptyPlates; // default=0
        private int idx;  // grid index, default=0
        private Plate[] plates;

        readonly List<Grid> all_grids = new();

        Dictionary<string, Brush> br = new()
        {
            { "B", Brushes.LightBlue }, 
            { "Y", Brushes.Yellow }, 
            { "G", Brushes.LightGreen },
            { "V", Brushes.Violet },
            { "P", Brushes.Pink },
            { "W", Brushes.White },
            { "C", Brushes.Cyan },
            { "K", Brushes.SandyBrown },
            { "O", Brushes.Orange },
            { "N", Brushes.OrangeRed },
            { "M", Brushes.Blue },
            { "T", Brushes.Turquoise }
        };

 

        public MainWindow()
        {
            InitializeComponent();

            Title = "Results";

            // Window dimensions
            Width = 1615;
            Height = 886;




            // <EXAMPLE INPUT>

            string[][] samples = new []
            {
                new string[] { "Sample-1", "Sample-2"},
                new string[] { "Sample-1", "Sample-2", "Sample-3", "Sample-5", "Sample-6", "Sample-7", "Sample-9","Sample-10", "Sample-11", "Sample-12", "Sample-13", "Sample-14", "Sample-15", "Sample-16", "Sample-17", "Sample-18"}, 
                new string[] { "Sample-1","Sample-2", "Sample-3"},
                new string[] { "Sample-2", "Sample-1","Sample-5", "Sample-7", "Sample-6", "Sample-9","Sample-10", "Sample-3" },
                new string[] { "Sample-1" }
            };


            string[][] reagents = new [] {
                    new string[] {"<Pink>", "<Green>", "<Noon>"},
                    new string[] {"<Blue>", "<ZReagent>", "<XReagent>", "<DReagent>"},
                    new string[] {"<Cyan>", "<Violet>", "<Reagent-A>"},
                    new string[] {"<Qreagent>"},
                    new string[] {"<Kiwi>", "<Turquoise>", "<Orange>"}
            };

            int[] replicates = new int[] { 1, 3, 3, 15, 5 };
            int plate_limit = 17;
            int plate_size = 384;
            //int plate_size = 96;





            // Main functionality
            Assemble(plate_size, samples, reagents, replicates, plate_limit);
        }

        static List<string> ValidateInputs(int plate_size, string[][] samples, string[][] reagents, int[] replicates, int plate_limit)
        {
            List<string> errors = new();

            // Plate limit

            if(plate_limit <= 0) { errors.Add("> Plate limit has to be greater than 0!"); }


            // Plate size check

            switch (plate_size)
            {
                case 96:
                case 384:
                    break;
                default:
                    errors.Add("> Invalid plate size specified !");
                    break;
            }


            // Reagent uniqueness check

            List<string> allReagents = new();
            int size = 0;

            for (int i = 0; i < reagents.GetLength(0); i++)
            {
                size += reagents[i].GetLength(0);

                for (int j = 0; j < reagents[i].GetLength(0); j++)
                {
                    allReagents.Add(reagents[i][j]);
                }

            }

            int altsize = allReagents.Distinct().ToArray().Length;
            if (altsize != size)
            {
                errors.Add("> Reagents not unique.");
            }


            // Sample uniqueness check

            bool samplesUnique = true;

            foreach(string[] s in samples)
            {
                if(s.Length != s.Distinct().Count())
                {
                    samplesUnique = false;
                }
            }

            if (!samplesUnique) errors.Add("> Experiment(s) contain duplicate samples.");


            // Basic list size check
            if ((samples.GetLength(0) != reagents.GetLength(0)) || (reagents.GetLength(0) != replicates.GetLength(0)))
            {
                errors.Add(string.Format("> Samples/reagents/replicates lengths mismatch ! ({0}/{1}/{2})", samples.GetLength(0), reagents.GetLength(0), replicates.GetLength(0)));
            }

            try
            {
                // Plate capacity check
                int total = 0;

                for (int i = 0; i < samples.GetLength(0); i++)
                {
                    total += samples[i].GetLength(0) * reagents[i].GetLength(0) * replicates[i];
                }
            }
            catch (IndexOutOfRangeException)
            {
                // Ignore exception on samples & reagents lengths mismatch
            }
            catch (Exception)
            {
                // Catch to show errors
                errors.Add("> Not enough plates !");
            }

            return errors;
        }

        private string[][][][] Assemble(int plate_size, string[][] samples, string[][] reagents, int[] replicates, int plate_limit)
        {
            // Validate inputs before starting
            List<string> er = new(ValidateInputs(plate_size, samples, reagents, replicates, plate_limit));

            if (er.Count > 0)
            {
                string errorText = "Fix the following error(s) before proceeding:" + Environment.NewLine + Environment.NewLine;
                string caption = "Warning";

                MessageBoxButton b = MessageBoxButton.OK;
                MessageBoxImage im = MessageBoxImage.Warning;

                foreach (string s in er)
                {
                    errorText += s + Environment.NewLine;
                }

                errorText += Environment.NewLine + Environment.NewLine + "Press OK to exit.";

                MessageBox.Show(errorText, caption, b, im, MessageBoxResult.OK);

                Application.Current.Shutdown();
                return null;
            }




            Random rnd = new();

            Dictionary<string, int> reagentIndices = new();
            Dictionary<string, int> ReagentSpaces = new();

            // Initialize plates
            plates = new Plate[plate_limit];

            if (plate_size == 96){
                for (int i = 0; i < plate_limit; i++) { plates[i] = new Plate(8, 12); }
            }
            else{
                for (int i = 0; i < plate_limit; i++) { plates[i] = new Plate(16, 24); }
            }


            // Fill reagentIndices with {reagent : experiment index it belongs to}
            for (int re = 0; re < reagents.GetLength(0); re++)
            {
                foreach (string r in reagents[re])
                {
                    reagentIndices.Add(r, re);
                }
            }


            // Fill dictionary with # of samples per reagent
            for (int i = 0; i < samples.GetLength(0); i++)
            {
                foreach (string re in reagents[i])
                {
                    ReagentSpaces.Add(re, samples[i].GetLength(0)); 
                }
            }


            
            // Sort by value
            var res = from e in ReagentSpaces
                      orderby e.Value descending
                      select e;


            // Assign back to ReagentSpaces
            ReagentSpaces = res.Select(e => (e.Key, e.Value))
                                           .ToDictionary(e => e.Key, e => e.Value);



            int rows = plates[0].Rows;
            int columns = plates[0].Columns;
            int limit = ReagentSpaces.Count;
            int plateIndex = 0;
            int exp_index = 0;

            string currentReagent;

            (int, int)[] coords;

            List<Shape> remaining = new();
            List<Shape> skipped = new();

            foreach (string k in ReagentSpaces.Keys)
            {
                exp_index = reagentIndices[k];

                // Is shape too wide and too tall ?
                if((replicates[exp_index] > plates[plateIndex].Columns) && (samples[exp_index].GetLength(0) > plates[plateIndex].Rows))
                {
                    List<Shape> tmp = new();

                    // Create one large shape
                    Shape largeShape = new(k,
                                           CreateShape(k,
                                                       samples[exp_index],
                                                       replicates[exp_index])
                                           );

                    // 1. Split by width
                    tmp = largeShape.SplitByWidth(plates[0].Columns);

                    // 2. Split resulting shapes by height
                    foreach (Shape shp in tmp)
                    {
                        foreach (Shape finalShape in shp.SplitByHeight(plates[0].Rows))
                        {
                            remaining.Add(finalShape);
                        }
                    }
                }

                // Does shape exceed plate width ?
                else if(replicates[exp_index] > plates[plateIndex].Columns)
                {
                    // Create one large shape
                    Shape largeShape = new(k,
                                           CreateShape(k, 
                                                       samples[exp_index], 
                                                       replicates[exp_index])
                                           );

                    // Split into multiple

                    var shp = largeShape.SplitByWidth(plates[0].Columns);

                    for (int i=0; i< shp.Count; i++)
                    {
                        remaining.Add(shp[i]);
                    }
                }

                // Does shape exceed plate height ?
                else if (samples[exp_index].GetLength(0) > plates[plateIndex].Rows)
                {
                    Shape largeShape = new(k,
                                           CreateShape(k,
                                                       samples[exp_index],
                                                       replicates[exp_index])
                                           );


                    // Split into multiple
                    var shp = largeShape.SplitByHeight(plates[0].Rows);

                    for(int i=0; i < shp.Count; i++)
                    {
                        remaining.Add(shp[i]);
                    }
                }
                
                else
                {
                    // Shape does not exceed single plate dimensions

                    Shape sh = new(k,
                                   CreateShape(k, samples[exp_index].ToArray(), replicates[exp_index])
                                  );

                    remaining.Add(sh);
                }
            }

            // Sort shapes
            remaining = remaining.OrderByDescending(x => x.GetRows() * x.GetColumns()).ToList();


            foreach(Shape currentShape in remaining)
            {
                for(int i=remaining.IndexOf(currentShape); i < remaining.Count; i++)
                {
                    Shape shp = remaining.ElementAt(i);
                }

                exp_index = reagentIndices[currentShape.Reagent]; // For referencing original order of elements from inputs
                currentReagent = currentShape.Reagent;

                bool shouldBreak = false;
                bool placedShape = false;

                for(int i=0; i < plates.Length; i++)
                {
                    if (shouldBreak) break;

                    shouldBreak = false;

                    Plate p = plates[i];

                    // Ignore full plates
                    if (p.Occupied() == p.Size()) continue;

                    // Acquire locations of empty spaces on current plate
                    coords = p.GetFreeSpaces();

                    if (coords.Length == 0) continue;


                    foreach ((int x, int y) in coords)
                    {
                        // Check location and place shape if valid
                        if (p.CheckShape(x, y, currentShape))
                        {
                            // CASE 1 - Location OK, shape fits

                            p.PlaceShape(x, y, currentShape);
                            placedShape = true;

                            shouldBreak = true;
                            break;
                        }
                    }
                }

                if(!placedShape) skipped.Add(currentShape);
            }

            // Placement done

            if (skipped.Count > 0)
            {
                // Display error containing what didn't fit
                string errorText = "Could not fit the following:" + Environment.NewLine + Environment.NewLine;
                string caption = "Warning";

                MessageBoxButton b = MessageBoxButton.OK;
                MessageBoxImage im = MessageBoxImage.Warning;

                for (int i = 0; i < skipped.Count; i++)
                {
                    errorText += ("-> " +
                                  skipped[i].Reagent +
                                  " - " +
                                  skipped[i].GetRows() +
                                  " samples,  " +
                                  skipped[i].GetColumns()+
                                  " replicates" +
                                  Environment.NewLine);
                }


                // Discard messagebox result 
                _ = MessageBox.Show(errorText, caption, b, im, MessageBoxResult.OK);
            }


            // Display results
            GenerateGrids(plates);
            ShowResults();


            // Console window
            AllocConsole();



            // Generate desired return format
            string[][][][] ret = new string[plates.Length][][][];

            Console.WriteLine("result = [");

            for (int i=0; i < ret.GetLength(0); i++)  // Plates
            {
                ret[i] = new string[plates[0].Rows][][];

                Console.WriteLine("  [");

                for(int j=0; j < ret[i].GetLength(0); j++) // Rows
                {
                    Console.Write("    [");

                    ret[i][j] = new string[plates[0].Columns][];

                    for(int k = 0; k < ret[i][j].GetLength(0); k++) // Columns
                    {
                        ret[i][j][k] = new string[2];  // Pair in each well

                        if(plates[i].Content[j][k] != null)
                        {
                            // Set [0] to sample name
                            ret[i][j][k][0] = plates[i].Content[j][k].IndexOf('<') != -1 ? plates[i].Content[j][k][..(plates[i].Content[j][k].IndexOf('<'))] : "n/a";
                            
                            // Set [1] to reagent name
                            ret[i][j][k][1] = plates[i].Content[j][k].IndexOf('<') != -1 ? plates[i].Content[j][k][plates[i].Content[j][k].IndexOf('<')..] : "n/a";

                            Console.Write("['" + ret[i][j][k][0] + "', '" + ret[i][j][k][1] + "'], ");
                        }
                        else {
                            Console.Write("null, ");
                        }
                    }
                    Console.WriteLine("]");
                }
                Console.WriteLine("  ], #Plate " + (i + 1) + "\n");
            }
            Console.WriteLine("]");

            return ret;
        }


        /// <summary>
        /// Generates content for creating shapes.
        /// </summary>
        /// <returns></returns>
        public static string[,] CreateShape(string reagent, string[] samples, int rep)
        {
            string[,] canvas = new string[samples.Length, rep];

            for(int y = 0; y < samples.Length; y++)
            {
                for(int x = 0; x < rep; x++)
                {
                    canvas[y, x] = samples[y] + reagent;
                }
            }
            return canvas;
        }


        /// <summary>
        /// Generates painted grids containing results from all plates.
        /// </summary>
        void GenerateGrids(Plate[] pl)
        {
            Dictionary<char, Brush> assignedBrushes = new();

            Brush randomBrush = Brushes.White;
            Random rand = new();

            int fontSize;


            for (int i=0; i< pl.Length; i++)
            {
                Plate p = pl[i];

                // Empty plates should not be accessible in results window
                if (p.Occupied() == 0) {
                    emptyPlates++;
                    continue;
                }

                Grid g = new()
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    ShowGridLines = true,
                    Width = 1600,
                    Height = 852
                };

                fontSize = p.Size() == 96 ? 20 : 16;


                ColumnDefinition[] cdef = new ColumnDefinition[p.Columns];
                RowDefinition[] rdef = new RowDefinition[p.Rows + 1]; // Extra row for buttons

                foreach (ColumnDefinition v in cdef)
                {
                    g.ColumnDefinitions.Add(new ColumnDefinition());
                }

                foreach (RowDefinition v in rdef)
                {
                    g.RowDefinitions.Add(new RowDefinition());
                }


                for (int y = 0; y < p.Rows ; y++)
                {
                    for (int x = 0; x < p.Columns; x++)
                    {
                        TextBlock t = new();
                        t.FontSize = fontSize;
                        t.FontWeight = FontWeights.Bold;
                        t.TextAlignment = TextAlignment.Center;
                        t.TextWrapping = TextWrapping.Wrap;



                        if (p.Content[y][x] == null)
                        {
                            t.Background = Brushes.White;
                        }
                        else
                        {
                            // Set text to sample name before '<', 'n/a' otherwise
                            t.Text = p.Content[y][x].IndexOf('<') != -1 ? p.Content[y][x][..(p.Content[y][x].IndexOf('<'))] : "n/a";

                            // Parse color from dictionary
                            string k = p.Content[y][x].IndexOf('<') != -1 ? p.Content[y][x][p.Content[y][x].IndexOf('<')+1].ToString() : "";

                            if (!br.ContainsKey(k))
                            {
                                // Assign random color
                                br[k] = new SolidColorBrush(Color.FromRgb((byte)rand.Next(0, 256), (byte)rand.Next(0, 256), (byte)rand.Next(0, 256)));     
                            }
                            
                            t.Background = br[k];
                        }       

                        Grid.SetRow(t, y);
                        Grid.SetColumn(t, x);

                        g.Children.Add(t);
                    }
                }


                // Make last row black
                for(int x=0; x < p.Columns; x++)
                {
                    TextBlock t = new();                   
                    t.Text = "";
                    t.Background = Brushes.Black;
                    
                    Grid.SetRow(t, p.Rows);
                    Grid.SetColumn(t, x);

                    g.Children.Add(t);
                }



                Button next = new();
                Button previous = new();

                next.Content = " Next ";
                previous.Content = " Prev ";

                next.FontWeight = FontWeights.Bold;
                previous.FontWeight = FontWeights.Bold;

                next.Click += new RoutedEventHandler(Next_Click);
                previous.Click += new RoutedEventHandler(Previous_Click);

                Grid.SetRow(next, p.Rows);
                Grid.SetRow(previous, p.Rows);

                Grid.SetColumn(next, p.Columns - 1);
                Grid.SetColumn(previous, 0);


                // Do not add unusable buttons
                if(i != 0)
                {
                    g.Children.Add(previous);
                }
                
                if(i != (plates.Length - 1))
                {
                    // Only add "next" button when there are more plates to show
                    if(pl[i + 1].Occupied() > 0)
                    {
                        g.Children.Add(next);
                    }
                }

                all_grids.Add(g);
            }
        }

        void ShowResults()
        {
            if(idx < all_grids.Count)
            {
                Content = all_grids[idx];
                Title = "Results page " + (idx + 1).ToString() + "/" + plates.Length;

                if (emptyPlates > 0)
                {
                    Title += "    (" + emptyPlates + " plates are empty...)";
                }
                
                Show();
            }           
        }

        void Next_Click(object sender, RoutedEventArgs e)
        {
            if (idx < plates.Length - 1)
            {
                idx++;
                ShowResults();
            }
        }

        void Previous_Click(object sender, RoutedEventArgs e)
        {
            if (idx > 0)
            {
                idx--;
                ShowResults();
            }
        }
    }

    public class Plate
    {
        public string[][] Content { get; set; }
        public int Rows { get; }
        public int Columns { get; }


        public Plate(int r, int c)
        {
            Rows = r;
            Columns = c;
            Content = new string[r][];

            for (int i = 0; i < Rows; i++) {
                Content[i] = new string[Columns];
            }
           
            // Initialize each well with null

            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    Content[y][x] = null;
                }
            }
        }


        /// <summary>
        /// Check if shape fits the location on plate 
        /// </summary>
        /// <param name="shape"> Shape to check </param>
        /// <returns></returns>
        public bool CheckShape(int x, int y, Shape shp)
        {
            string[,] shape = shp.Content;

            // Save initial y coordinate
            int _y = y;

            for (int i = 0; i < shape.GetLength(1); i++)   // For each replicate (column)
            {
                for (int j = 0; j < shape.GetLength(0); j++)   // For each sample (row)
                {
                    // Return if out of bounds
                    if (y >= Rows) { return false; }

                    if (x >= Columns) { return false; }

                    if(Content[y][x] != null){ return false; }

                    y++;
                }
                y = _y;  // Reset to inital row index
                x++;
            }

            return true;
        }

        /// <summary>
        /// Places shape at given coordinates
        /// </summary>
        public void PlaceShape(int x, int y, Shape shp)
        {
            if(Content[y][x] != null) { return; }

            string[,] shape = shp.Content;

            int _y = y;

            for(int i = 0; i < shape.GetLength(1); i++)  // Columns
            {
                for(int j = 0; j < shape.GetLength(0); j++)  // Rows
                {
                    Content[y][x] = shape[j,i];
                    y++;
                }
                y = _y;
                x++;
            }
        }

        /// <summary>
        /// Returns count of occupied spaces
        /// </summary>
        public int Occupied()
        {
            int res = 0;

            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    if (Content[y][x] != null) { res += 1; }
                }
            }
            return res;
        }

        /// <summary>
        /// Returns locations of free spaces
        /// </summary>
        public (int,int)[] GetFreeSpaces()
        {
            int c = 0;
            (int, int)[] spaces = new (int, int)[Size() - Occupied()];

            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    if (Content[y][x] == null) {
                        spaces[c] = (x,y);
                        c++;
                    }
                }
            }
            return spaces;
        }

        public int Size()
        {
            return Columns * Rows;
        }
    }


    public class Shape
    {
        public string[,] Content { get; set; }

        public string Reagent { get; }

        public Shape(string r, string[,] co)
        { 
            Reagent = r;
            Content = co;
        }

        public int GetRows()
        {
            return Content.GetLength(0);
        }

        public int GetColumns()
        {
            return Content.GetLength(1);
        }

        /// <summary>
        /// Splits shape by width
        /// </summary>
        /// <param name="plateColumns"> Width to split at </param>
        /// <returns> List of resulting shapes </returns>
        public List<Shape> SplitByWidth(int plateColumns)
        {
            List<Shape> res = new();

            int c = GetColumns() / plateColumns; // How many full plate widths
            int rem = GetColumns() % plateColumns; // Remainder


            // 1. Create first shape

            string[] column = new string[GetRows()];

            for (int i = 0; i < column.Length; i++) // Copy first column (samples)
            {
                column[i] = Content[i, 0];
            }

            string[,] canvas = new string[column.Length, plateColumns];

            for (int y = 0; y < column.Length; y++)
            {
                for (int x = 0; x < plateColumns; x++)
                {
                    canvas[y, x] = column[y];
                }
            }


            // 2. Repeat shape if necessary
            if (c > 0)
            {
                for (int i = 0; i < c; i++)
                {
                    res.Add(new Shape(Reagent, canvas));
                }
            }


            // 3. Create shape from remaining columns
            canvas = new string[column.Length, rem];

            for (int y = 0; y < column.Length; y++)
            {
                for (int x = 0; x < rem; x++)
                {
                    canvas[y, x] = column[y];
                }
            }

            if (rem > 0)
            {
                res.Add(new Shape(Reagent, canvas));
            }

            return res;
        }

        /// <summary>
        /// Splits shape by height
        /// </summary>
        /// <param name="plateRows"> Height to split at </param>
        /// <returns> List of resulting shapes </returns>
        public List<Shape> SplitByHeight(int plateRows)
        {
            List<Shape> res = new();

            int c = GetRows() / plateRows; // How many full plate widths
            int rem = GetRows() % plateRows; // Remainder
            int sampleIndex = -1;

            for (int i = 0; i < c; i++)
            {
                // 1. Create content for full shapes
                string[,] canvas = new string[plateRows, GetColumns()];

                for (int y = 0; y < plateRows; y++) // Iterate rows
                {
                    sampleIndex++;
                    for (int x = 0; x < GetColumns(); x++) // Iterate columns
                    {
                        canvas[y, x] = Content[sampleIndex, x];
                    }
                }

                // 2. Add shape
                res.Add(new Shape(Reagent, canvas));
            }


            // 3. Process remaining partial shape
            if (rem > 0)
            {
                // 4. Create content
                string[,] canvas = new string[rem, GetColumns()];

                for (int y = 0; y < rem; y++) // Iterate rows
                {
                    sampleIndex++;
                    for (int x = 0; x < GetColumns(); x++) // Iterate columns
                    {
                        canvas[y, x] = Content[sampleIndex, x];
                    }
                }

                // 5. Add shape
                res.Add(new Shape(Reagent, canvas));
            }

            return res;
        }
    }
}
