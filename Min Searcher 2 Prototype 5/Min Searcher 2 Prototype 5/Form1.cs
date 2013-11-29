using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml;

namespace Min_Searcher_2_Prototype_5
{
    public partial class MainForm : Form
    {
        #region memberData
        ExcelCreator outputGenerator;
        private List<int> checkedBins = new List<int>();//used to keep track of what bins are checked in a list box
        private List<int> combinedBins = new List<int>();//keeps track of bins to be combined
        private bool exited = false;//checks to see if the user exited
        private bool firstGraph = true;//keeps track of when graph is first made
        //names of files and programs used in program can be changed here
        //the gnuplot names have a lot of slashes that is because gnuplot requires double slashes
        private string programFolderPath = @"C:\Users\GEARHARTJJ1\Documents\GitHub\MinSearchProgram\Min Searcher 2 Prototype 5\";
        private string generatedFilesFolderGnuplotPath = "\"C:\\\\Users\\\\gearhartjj1\\\\documents\\\\GitHub\\\\MinSearchProgram\\\\Min Searcher 2 Prototype 5\\\\Generated Files\\\\";
        private string pathForTemplateOutput = @"C:\Users\gearhartjj1\documents\GitHub\MinSearchProgram\Min Searcher 2 Prototype 5\\";
        private string templateOutputName = "outputTemplate.xlsx";
        private string gnuplotLocation = @"C:\Program Files (x86)\gnuplot\bin\gnuplot.exe";
        //locations for graph data to be saved in
        private string scatterPlotDataLocation;
        private string surfacePlotDataLocation;//only the path to the folder so that multiple files can be made
        private string binGraphEnergyDataLocation;//location where the bin graphs data are stored
        private string binGraphAlphaDataLocation;
        private string BGAgnu;//location specific for gnuplot needs
        private string BGEgnu;
        private Image binGraphA;//image for alpha graph
        private Image binGraphE;//image for energy graph
        //these ones are for gnuplot
        private string surfacePlotData; //need to append notitle
        private string scatterPlotData;
        //locations to store gridgen input files and min output files
        private string inputFileLocation;//only has the location
        private string minOutputFileLocation;//only has location
        //folder mouse data is saved in, used by system watcher in a thread function
        private string mouseDataLocation;
        //mouse data needs to be in its own folder or the file watcher will mess up if another file is updated in the same folder
        //used for the file that gnuplot saves mouse data in
        private string mouseDataFile;//has just the location
        private string gridgenProgramName;
        private string minProgramName;

        //most recent graph images
        private Image recentScatterPlot;

        private GraphingDataValues graphData;
        public int MinRoot1 { get; set; }//these are roots used in minsearch, can be independent from graph roots
        public int MinRoot2 { get; set; }
        public double MinRealRange { get; set; }//these are the real and imaginary ranges for min searches
        public double MinImagRange { get; set; }
        public static int NumDataPoints { get; set; }
        public static int NumRoots { get; set; }
        private int masterDataLocation { get; set; }
        private int oldDataLocation { get; set; }//holds onto old location in case the action is canceled
        private double minX { get; set; }
        private double maxX { get; set; }
        private int numberOfGraphs = 5;
        //This is all the data that comes from the input file, arranged in a way
        //So that each data point has numRoots data values
        private List<MultipleDataPoints> totalData = new List<MultipleDataPoints>();
        //This is the master set that the subsets will be created from
        private List<IndividualDataPoint> masterDataSet = new List<IndividualDataPoint>();
        //bin data stored during the program
        private List<Bin> bins = new List<Bin>();
        private MinSearchData recentSearchData = new MinSearchData();
        //hold names of bins for the combo box drop down
        Dictionary<int, string> binNames = new Dictionary<int, string>();
        DataTable displayData = new DataTable();//table that contains data to be displayed during the process
        DataTable statsData = new DataTable();//table that displays averages and stddev during the process
        private List<string> surfaceGraphDataNames = new List<string>(); //names of data files of surface graph data
        private List<Thread> threadsRunning = new List<Thread>(); //list of threads running surface graphs
        //keeps track of whether or not the auto save feature is on
        private bool autoSave = false;
        //keeps track of what bin to use
        private int autoSaveBin = 0;
        //keeps track if the user has to choose the autosave bin
        private bool chooseAutoSaveBin = true;

        private int maxGraphsOnScreen;
        private const int MIN_GRAPH_SIZE = 150;
        #endregion

        //functions used to manipulate windows on the screen
        #region moveWindowFunctions
        //imported user32.dll functions for moving windows on screen
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll",SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        
        static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true)]
        static extern IntPtr FindWindowByCaption(IntPtr ZeroOnly, string lpWindowName);

        private static WINDOWPLACEMENT GetPlacement(IntPtr hwnd)
        {
            WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(placement);
            GetWindowPlacement(hwnd, ref placement);
            return placement;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowPlacement(
            IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        internal struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public ShowWindowCommands showCmd;
            public System.Drawing.Point ptMinPosition;
            public System.Drawing.Point ptMaxPosition;
            public System.Drawing.Rectangle rcNormalPosition;
        }

        internal enum ShowWindowCommands : int
        {
            Hide = 0,
            Normal = 1,
            Minimized = 2,
            Maximized = 3,
        }
        #endregion
        
        public MainForm()
        {
            //could eliminate this by having the code reference the programFolderPath variable and do the additions itself but I dont feel like making the changes
            //create names of file locations
            scatterPlotDataLocation = programFolderPath + "Generated Files\\scatterPlot.txt";
            binGraphEnergyDataLocation = programFolderPath + "Generated Files\\energyBinGraph.txt";
            binGraphAlphaDataLocation = programFolderPath + "Generated Files\\alphaBinGraph.txt";
            surfacePlotDataLocation = programFolderPath + "Generated Files\\";
            inputFileLocation = programFolderPath + "Generated Files\\";
            minOutputFileLocation = programFolderPath + "Generated Files\\";
            mouseDataLocation = programFolderPath + "Generated Files\\MouseData\\";
            //paths for gnuplot to follow
            surfacePlotData = generatedFilesFolderGnuplotPath;
            scatterPlotData = generatedFilesFolderGnuplotPath + "scatterPlot.txt\"";
            BGAgnu = generatedFilesFolderGnuplotPath + "alphaBinGraph.txt\"";
            BGEgnu = generatedFilesFolderGnuplotPath + "energyBinGraph.txt\"";
            mouseDataFile = generatedFilesFolderGnuplotPath + "MouseData\\\\";
            //program file names
            gridgenProgramName = programFolderPath + "Program Files\\GridgenWin64.exe";
            minProgramName = programFolderPath + "Program Files\\PaacWin64.exe";

            InitializeComponent();
            graphData = new GraphingDataValues();
            //temp values for start and end for bin graphs
            graphData.StartRRange = 0;
            graphData.StartIRange = 0;
            graphData.EndRRange = 2;
            graphData.EndIRange = 2;
            graphData.polynomials = new List<int>();
            this.binOptionsComboBox.DataSource = new BindingSource(binNames, null);
            MinRealRange = .05;
            MinImagRange = .05;
            dataAndGraphChanger.SelectTab("graphPage");//puts interface on graph
            interfaceChanger.SelectTab("blankPage");//puts rest of interface on blank tab
            this.mouseDataLabel.Text = "";//makes these labels gone
            this.derivativeDataLabel.Text = "";
            this.stationaryPointLabel.Text = "";
            this.stationaryEnergyLabel.Text = "";
            displayData.Columns.Add("Name",typeof(string));
            displayData.Columns.Add("Point", typeof(int));
            displayData.Columns.Add("Real Alpha", typeof(double));
            displayData.Columns.Add("Imag Alpha", typeof(double));
            displayData.Columns.Add("Real E", typeof(double));
            displayData.Columns.Add("Imag E", typeof(double));
            dataGridView1.DataSource = displayData;
            statsData.Columns.Add("Name", typeof(string));
            statsData.Columns.Add("Type", typeof(string));
            statsData.Columns.Add("Alpha Avg", typeof(double));
            statsData.Columns.Add("Alpha StdDev", typeof(double));
            statsData.Columns.Add("E Avg", typeof(double));
            statsData.Columns.Add("E StdDev", typeof(double));
            statsDataGridView.DataSource = statsData;
            //stop user from being able to sort rows
            foreach (DataGridViewColumn column in this.dataGridView1.Columns)
            {
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
            }
            foreach (DataGridViewColumn column in this.statsDataGridView.Columns)
            {
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
            }

            //calculate the max number of graphs they can generate
            if (Screen.AllScreens.Length > 1)//external monitor is attached
            {
                int graphsPerRow = Screen.AllScreens[1].Bounds.Width / MIN_GRAPH_SIZE;
                int totalRows = Screen.AllScreens[1].Bounds.Height / MIN_GRAPH_SIZE;
                maxGraphsOnScreen = graphsPerRow * totalRows;
            }
            else
            {
                int graphsPerRow = (Screen.AllScreens[0].Bounds.Width - this.Size.Width) / MIN_GRAPH_SIZE;
                int totalRows = Screen.AllScreens[0].Bounds.Height / MIN_GRAPH_SIZE;
                maxGraphsOnScreen = graphsPerRow * totalRows;
            }

        }

        //Functions that are essential and mostly independent from the interface for the most part
        #region mainProgramFunctions
        //used to click a point on graph if it only opened once, no longer used at all because the graph is kept open
        private GraphPoint clickPoint()
        {
            double x = 0;
            double y = 0;
            //start gnuplot and get point click data
            string programName = gnuplotLocation;
            Process gnuPlot = new Process();
            gnuPlot.StartInfo.FileName = programName;
            gnuPlot.StartInfo.UseShellExecute = false;
            gnuPlot.StartInfo.RedirectStandardInput = true;
            gnuPlot.StartInfo.CreateNoWindow = true;
            gnuPlot.Start();
            StreamWriter gnuPlotInput = gnuPlot.StandardInput;
            gnuPlotInput.WriteLine("set pm3d map");
            gnuPlotInput.Flush();
            string graphFileName = string.Format("{0} notitle", surfacePlotData);
            gnuPlotInput.WriteLine(String.Format("splot {0}", graphFileName));
            gnuPlotInput.Flush();
            gnuPlotInput.WriteLine("set print \"mouseData.txt\"");
            gnuPlotInput.Flush();
            gnuPlotInput.WriteLine("pause mouse; print MOUSE_X, MOUSE_Y; unset print;exit;");
            gnuPlotInput.Flush();
            gnuPlot.WaitForExit();
            //gnuPlotInput.WriteLine("bind close 'exit gnuplot'");
            gnuPlotInput.Close();
            gnuPlot.Close();
            StreamReader mouseDataReader = new StreamReader("mouseData.txt");
            string data = mouseDataReader.ReadToEnd();
            string[] dataPoints = data.Split(' ');
            try
            {
                x = double.Parse(dataPoints[0]);
                y = double.Parse(dataPoints[1]);
            }
            catch
            {
                MessageBox.Show("mouse data failed to read properly");
            }
            mouseDataReader.Close();
            return new GraphPoint(x, y);
        }
        //automatically updates the values for the input file, and regenerates it and uses it for the min program
        //then it writes the results to a file and uses string parsing to get the data values necessary
        //these are saved in local doubles that should later be used in the "bins"
        //also now takes a dataLocation value which will be kept track with by each thread
        private void searchForMin(double realGuess, double imaginaryGuess, int dataLocation, string inputFile = "gridgen.inp")
        {
            //debug info to make sure that the right polynomial and set are being used
            Console.WriteLine("poly: " + graphData.Polynomial + " set: " + dataLocation);
            //data gathered by the min program to be stored elsewhere later
            double derivative = 0, statPointReal = 0, statPointImag = 0, statEReal = 0, statEImag = 0;
            GraphingDataValues originalData = graphData;//saves original data
            //now going to change data
            graphData.RealGuess = realGuess;
            graphData.ImaginaryGuess = imaginaryGuess;
            graphData.Tolerance = "1.0D-25";
            graphData.ProgramType = "minm";
            graphData.RealRange = MinRealRange;
            graphData.ImaginaryRange = MinImagRange;
            graphData.RootInUse1 = MinRoot1;//makes the roots be the min roots
            graphData.RootInUse2 = MinRoot2;//min roots can be the same as graph roots
            generateInputFile(dataLocation, inputFile);//generates minm input file

            //now going to call paacWin64.x to run the min search
            Process minProgram = new Process();
            minProgram.StartInfo.FileName = minProgramName;
            //minProgram.StartInfo.Arguments = commandLine;
            minProgram.StartInfo.CreateNoWindow = true;
            minProgram.StartInfo.UseShellExecute = false;
            minProgram.StartInfo.RedirectStandardInput = true;
            minProgram.StartInfo.RedirectStandardOutput = true;

            StreamWriter outputWriter = new StreamWriter(minOutputFileLocation + "min" + dataLocation + ".out");
            AutoResetEvent outputWaitHandle = new AutoResetEvent(false);

            minProgram.OutputDataReceived += (sender, e) =>
            {
                if (e.Data == null)
                {
                    outputWaitHandle.Set();
                }
                else
                {
                    outputWriter.WriteLine(e.Data);
                }
            };

            minProgram.Start();

            StreamWriter inputWriter = minProgram.StandardInput;
            StreamReader inputFileReader = new StreamReader(inputFileLocation + inputFile);
            inputWriter.Write(inputFileReader.ReadToEnd());
            string text;
            while ((text = inputFileReader.ReadLine()) != null)
            {
                inputWriter.WriteLine(text);
            }
            minProgram.BeginOutputReadLine();

            minProgram.WaitForExit();
            outputWaitHandle.Close();//always close stuff

            outputWriter.Close();
            inputFileReader.Close();
            inputWriter.Close();
            minProgram.Close();

            //resets original data
            graphData = originalData;
            //get end of min.out file, an attempt to get the desired data values from the file
            string endData = "";
            using (StreamReader reader = new StreamReader(minOutputFileLocation + "min" + dataLocation + ".out"))
            {//gets the end of the min file where the desired data is stored
                reader.BaseStream.Seek(-228, SeekOrigin.End);
                endData = reader.ReadToEnd();
                //endData = endData.Replace(System.Environment.NewLine, "");
                endData = endData.Replace(" ", "");
            }
            string[] dataValues = endData.Split('\n');//splits up the values by new lines
            dataValues = dataValues.Where(val => val.Trim() != "TheStationarypointwas"
                && val.Trim() != "TheStationaryEnergywas" && val.Trim() != "").ToArray();//This gets rid of every non-data value
            //MessageBox.Show(endData);
            try
            {
                derivative = double.Parse(dataValues[0]);
            }
            catch
            {
                MessageBox.Show("Derivative value failed to be obtained");
            }
            //now divide the string for the stationary point into the real and imaginary values
            string[] statPointValues = dataValues[1].Split('(', ',', ')');
            statPointValues = statPointValues.Where(val => val.Trim() != "").ToArray();//removes extra space values
            try
            {
                statPointReal = double.Parse(statPointValues[0]);
                statPointImag = double.Parse(statPointValues[1]);
            }
            catch
            {
                MessageBox.Show("The stationary point values failed to be obtained");
            }
            //now divide the string for the stat E into real and imag values
            string[] statEValues = dataValues[2].Split('(', ',', ')');
            statEValues = statEValues.Where(val => val.Trim() != "").ToArray();//removes extra space values
            try
            {
                statEReal = double.Parse(statEValues[0]);
                statEImag = double.Parse(statEValues[1]);
            }
            catch
            {
                MessageBox.Show("The stationary point values failed to be obtained");
            }
            //saves data in private member
            recentSearchData.DerivativeValue = derivative;
            recentSearchData.ImagAlpha = statPointImag;
            recentSearchData.ImagE = statEImag;
            recentSearchData.PolynomialOrder = graphData.Polynomial;
            recentSearchData.RealAlpha = statPointReal;
            recentSearchData.RealE = statEReal;
            recentSearchData.RootUsed1 = graphData.RootInUse1;
            recentSearchData.RootUsed2 = graphData.RootInUse2;
            recentSearchData.SetUsed = dataLocation;
            graphData = originalData;
        }
        private void readInitialData(string fileName)//this reads input file into data structure
        {//assumes that the data file has one x and y pair per line to the end
            totalData.Clear();
            StreamReader inputFileReader = new StreamReader(fileName);
            graphData.Title = fileName;
            string text;
            string[] parts = new string[2];
            List<IndividualDataPoint> sortedList = new List<IndividualDataPoint>();
            while ((text = inputFileReader.ReadLine()) != null && text != "")
            {
                parts = System.Text.RegularExpressions.Regex.Split(text, "\t");
                double x = double.Parse(parts[0]);
                double y = double.Parse(parts[1]);
                sortedList.Add(new IndividualDataPoint(x, y));
                Array.Clear(parts, 0, parts.Length);//empties the array
            }
            //sorts list by x and y to get the data in the proper order
            sortedList = sortedList.OrderBy(C => C.XValue).ThenBy(C => C.YValue).ToList();
            int location = 0;
            NumRoots = 1;
            //calculates the number of roots
            //work under assumption that at least for the first x value all roots will have data
            while (sortedList[location].XValue == sortedList[location+1].XValue)
            {
                location++;
                NumRoots++;
            }
            //makes sure there is enough data for all roots by inserting dummy data for roots that are incomplete
            //problem is is what if it is a middle root that is missing data not the highest root
            int end = sortedList.Count;
            for (int i = 0; i < end;)
            {
                double firstX = sortedList[i].XValue;
                for (int j = 0; j < NumRoots; j++)
                {
                    if (i >= sortedList.Count)//if it reaches the end withing the loop
                    {
                        if (j < NumRoots)//this means that the last data point is missing data
                        {
                            sortedList.Insert(i, new IndividualDataPoint(firstX, 0));
                        }
                        break;
                    }
                    if (sortedList[i].XValue != firstX)//so basically if the data changes too quickly it realizes there are missing points
                    {//the number of roots was determined earlier by assuming that the first data point should have data for all roots or it is stupid...
                        sortedList.Insert(i, new IndividualDataPoint(firstX, 0));
                        end = sortedList.Count;
                    }
                    i++;
                }
            }

            //calculates the number of data points by dividing the number of points by the number of roots
            NumDataPoints = sortedList.Count / NumRoots;

            //things are fine problem is that some of the roots are missing data
            for (int i = 0; i < NumDataPoints; i++)
            {
                totalData.Add(new MultipleDataPoints());
                for (int j = 0; j < NumRoots; j++)
                {
                    double x = sortedList[(i * NumRoots) + j].XValue;
                    double y = sortedList[(i * NumRoots) + j].YValue;
                    totalData[i].data.Add(new IndividualDataPoint(x, y));
                }
            }
            inputFileReader.Close();
        }
        private void createMasterData(double minX, double maxX, params int[] roots)
        {
            masterDataSet.Clear();
            for (int i = 0; i < NumDataPoints; i++)
            {
                for (int j = 0; j < roots.Length; j++)
                {//the if checks to make sure that the x value is in the desired range
                    //adjustments are not necessary for the root value because the x is the same for all roots
                    if (totalData[i].data[j].XValue > .001 && totalData[i].data[j].XValue >= minX && totalData[i].data[j].XValue <= maxX)
                    {
                        //this puts in the values for the particular roots
                        masterDataSet.Add(new IndividualDataPoint(totalData[i].data[roots[j] - 1].XValue, totalData[i].data[roots[j] - 1].YValue));
                    }
                }
            }
            graphData.NumRootsUsed = roots.Length;
        }
        //start point determins where in the master data to start putting in data
        //all values used are in the graphData member and should be changed before generating file
        //will throw an exception if the start value causes it to not be enough data, need to use in try/catch block
        private void generateInputFile(int startPoint, string inputFileName = "gridgen.inp")
        {
            //determines how much data is necessary for input file
            int numDataPoints = (graphData.NumRootsUsed + 1) * (graphData.Polynomial + 1) - 1;
            if (startPoint + numDataPoints > masterDataSet.Count)
            {
                throw (new Exception());//throws an exception if they hit the end of the master data set and cant fill a file
            }
            StreamWriter inputFileWriter = new StreamWriter(inputFileLocation + inputFileName);
            inputFileWriter.WriteLine(graphData.Title);
            inputFileWriter.WriteLine(graphData.NumRootsUsed);
            for (int i = 0; i <= graphData.NumRootsUsed; i++)//this is <= because it has to be written numRoots+1 times
            {
                inputFileWriter.Write(String.Format("{0} ", graphData.Polynomial));
            }
            inputFileWriter.WriteLine();
            inputFileWriter.WriteLine("nolstq");//not sure what it means but is crucial to gridgen.exe
            for (int i = startPoint; i < numDataPoints + startPoint; i++)
            {//puts in the data from the master set
                inputFileWriter.WriteLine(String.Format("{0} {1}", masterDataSet[i].XValue, masterDataSet[i].YValue));
            }
            //next is grid or min
            inputFileWriter.WriteLine(graphData.ProgramType);
            inputFileWriter.WriteLine("nobranch");
            inputFileWriter.WriteLine(String.Format("{0} {1}", graphData.RootInUse1, graphData.RootInUse2));//this may change if more roots can be used
            inputFileWriter.WriteLine(graphData.Tolerance);
            inputFileWriter.WriteLine(String.Format("({0},{1})", graphData.RealGuess, graphData.ImaginaryGuess));
            inputFileWriter.WriteLine(graphData.RealRange);
            inputFileWriter.WriteLine(graphData.ImaginaryRange);
            inputFileWriter.WriteLine(graphData.GridSize);//size of grid access could become a variable if necessary
            //uncomment to use new versions
            //inputFileWriter.WriteLine(String.Format("{0} {1}"), graphData.StartRRange, graphData.EndRRange);
            //inputFileWriter.WriteLine(String.Format("{0} {1}"), graphData.StartIRange, graphData.EndIRange);
            inputFileWriter.Close();
        }
        private void createSurfaceGraph(string fileName, string inputFileName = "gridgen.inp")//only generates data for surface graph
        {
            //currently written to file, might be possible to change to send to data structure if necessary
            /*
             * An explanation of how the input output redirection works for future reference:
             * The first method used involved calling the command prompt with a the exe name and arguments as the arguments for the process
             * this worked but would not allow we to choose the locations of the files so the program would have to be run in the same program
             * Eventually I changed this:
             * Now the exe is called like a normal process with redirected input and output
             * the input is redirected by using a streamreader to give the input the file one line at a time
             * then the output is eventually put to a file using a streamwriter by diverting the output
             * this was complicated bc the output was larger than the output buffer
             * now it is sent to the file chunks at a time using asynchronous writing/reading and by raising a datarecieved event in a lambda expression
             * Last bit was with help from
             * http://stackoverflow.com/questions/139593/processstartinfo-hanging-on-waitforexit-why
             * */
            Process gridGen = new Process();
            gridGen.StartInfo.FileName = gridgenProgramName;
            gridGen.StartInfo.UseShellExecute = false;
            gridGen.StartInfo.CreateNoWindow = true;
            gridGen.StartInfo.RedirectStandardInput = true;
            gridGen.StartInfo.RedirectStandardOutput = true;

            StreamWriter outputWriter = new StreamWriter(fileName);
            AutoResetEvent outputWaitHandle = new AutoResetEvent(false);

            gridGen.OutputDataReceived += (sender, e) =>
            {
                if (e.Data == null)
                {
                    outputWaitHandle.Set();
                }
                else
                {
                    outputWriter.WriteLine(e.Data);
                }
            };

            gridGen.Start();
            StreamWriter inputWriter = gridGen.StandardInput;
            StreamReader inputFileReader = new StreamReader(inputFileLocation + inputFileName);
            inputWriter.Write(inputFileReader.ReadToEnd());
            string text;
            while ((text = inputFileReader.ReadLine()) != null)
            {
                inputWriter.WriteLine(text);
            }
            gridGen.BeginOutputReadLine();
            gridGen.WaitForExit();
            outputWaitHandle.Close();//always close stuff
            
            outputWriter.Close();
            inputFileReader.Close();
            inputWriter.Close();
            gridGen.Close();

            //check to see if gridgen executed properly
            using (StreamReader reader = new StreamReader(fileName))
            {
                if (reader.ReadToEnd() == "")
                {
                    throw(new Exception());//throws an exception if it is empty
                }
            }
            
        }
        private void createScatterPlot(bool useTotalData)
        {
            StreamWriter scatterPlotWriter = new StreamWriter(scatterPlotDataLocation);
            //C:\Users\gearhartjj1\documents\college\summer 2013 job\
            if (useTotalData)//these generate the files used for the scatter plot, could be changed to put data in a structure if necessary
            {
                //this puts the first value in with a color value different from the rest to make the initial colors better
                scatterPlotWriter.WriteLine(string.Format("{0} {1} {2}", totalData[0].data[0].XValue, totalData[0].data[0].YValue, 0));
                foreach (MultipleDataPoints dataPoint in totalData)
                {//this good stuff creates the file with data for a scatter plot
                    for (int i = 0; i < dataPoint.data.ToArray().Length; i++)
                    {
                        scatterPlotWriter.WriteLine(string.Format("{0} {1} {2}", dataPoint.data[i].XValue, dataPoint.data[i].YValue, 2));
                    }
                }
            }
            else
            {
                int numDataPoints = (graphData.NumRootsUsed + 1) * (graphData.Polynomial + 1) - 1;
                if (graphData.Polynomial == 0)
                {
                    numDataPoints = 0;//if the poly is 0 then there is no chosen value so there is no number of data points necessary
                    //again a dummy value to adjust the coloring
                    scatterPlotWriter.WriteLine(string.Format("{0} {1} {2}", masterDataSet[0].XValue, masterDataSet[0].YValue, 0));
                }
                for (int i = 0; i < masterDataSet.Count; i++)
                {
                    if (i >= masterDataLocation && i < masterDataLocation + numDataPoints)
                        scatterPlotWriter.WriteLine(string.Format("{0} {1} {2}", masterDataSet[i].XValue, masterDataSet[i].YValue, 2));
                    else
                        scatterPlotWriter.WriteLine(string.Format("{0} {1} {2}", masterDataSet[i].XValue, masterDataSet[i].YValue, 2.1));
                }
            }
            scatterPlotWriter.Close();
            //this is going to plot data in gnuplot, my be changed later if diff grapher is used
            string fileName = gnuplotLocation;
            Process gnuplotProcess = new Process();
            gnuplotProcess.StartInfo.FileName = fileName;
            gnuplotProcess.StartInfo.UseShellExecute = false;
            gnuplotProcess.StartInfo.RedirectStandardInput = true;
            gnuplotProcess.StartInfo.RedirectStandardOutput = true;//used to get image
            gnuplotProcess.StartInfo.CreateNoWindow = true;//suppresses command window
            gnuplotProcess.Start();
            StreamWriter gnuplotInput = gnuplotProcess.StandardInput;
            StreamReader gnuplotOutput = gnuplotProcess.StandardOutput;//used to get image
            gnuplotInput.WriteLine("set terminal pngcairo size 500,400");//used to get image
            gnuplotInput.Flush();
            gnuplotInput.WriteLine(String.Format("set view map; set palette rgbformulae 33,13,10; splot {0} with points palette notitle;exit;", scatterPlotData));
            recentScatterPlot = Image.FromStream(gnuplotOutput.BaseStream);//used to get image
            graphPictureBox1.Image = recentScatterPlot;
            gnuplotInput.Flush();
            gnuplotInput.WriteLine("exit");
            gnuplotInput.Close();
            gnuplotProcess.Close();
        }
        //saves data in displan and calls function to update display
        private void saveData(int binNumber, MinSearchData data)
        {
            Console.WriteLine("saving to " + binNumber);
            if (binNumber >= bins.ToArray().Length)
            {//user wants to create a new bin
                if (bins.Count == 0)//turn on the data viewers if this is the first input
                {
                    this.dataGridView1.Visible = true;
                    this.statsDataGridView.Visible = true;
                    this.savedDataLabel.Visible = true;
                    this.statsDataLabel.Visible = true;
                }
                bins.Add(new Bin());
                bins[binNumber].binData.Add(data);
                this.statsDataGridView.BeginInvoke(new MethodInvoker(() =>
                    {
                        updateStats(binNumber);
                    }
                ));
                //doesn't update display if a new bin is added because the name has to be changed first
                //update should be called outside of this function when creating new bin
            }
            else
            {
                bins[binNumber].binData.Add(data);
                this.statsDataGridView.BeginInvoke(new MethodInvoker(() =>
                {
                    updateStats(binNumber);
                }
                ));
            }
            createBinGraphs();
        }
        //used to update the display of bin data including averages and std devs
        private void updateBinAveragesAndStdDev()
        {
            statsData.Clear();
            for (int i = 0; i < bins.Count; i++)
            {
                updateStats(i);
            }
        }
        private void updateStats(int binNumber)
        {
            binNumber *= 2;//has to multiply by 2 because every bin has two rows in this table
            if (statsData.Rows.Count >= (binNumber + 1))
            {
                statsData.Rows.RemoveAt(binNumber);
                //number does not change because the index was just decreased by one
                statsData.Rows.RemoveAt(binNumber);//because every bin requires two rows
            }
            Bin bin = bins[binNumber / 2];
            List<double> realETempData = new List<double>();
            List<double> imagETempData = new List<double>();
            List<double> realAlphaTempData = new List<double>();
            List<double> imagAlphaTempData = new List<double>();
            //updates average and std dev values
            foreach (MinSearchData dataPoint in bin.binData)
            {
                realETempData.Add(dataPoint.RealE);
                imagETempData.Add(dataPoint.ImagE);
                realAlphaTempData.Add(dataPoint.RealAlpha);
                imagAlphaTempData.Add(dataPoint.ImagAlpha);
            }
            bin.AverageRealE = realETempData.Average();
            bin.AverageImagE = imagETempData.Average();
            bin.StdDevRealE = calculateStandardDeviation(realETempData);
            bin.StdDevImagE = calculateStandardDeviation(imagETempData);
            bin.AverageRealAlpha = realAlphaTempData.Average();
            bin.AverageImagAlpha = imagAlphaTempData.Average();
            bin.StdDevRealAlpha = calculateStandardDeviation(realAlphaTempData);
            bin.StdDevImagAlpha = calculateStandardDeviation(imagAlphaTempData);
            realETempData.Clear();
            imagETempData.Clear();
            realAlphaTempData.Clear();
            imagAlphaTempData.Clear();
            DataRow myRow = statsData.NewRow();
            
            myRow[0] = bin.BinName;
            myRow[1] = "Real";
            myRow[2] = bin.AverageRealAlpha;
            myRow[3] = bin.StdDevRealAlpha;
            myRow[4] = bin.AverageRealE;
            myRow[5] = bin.StdDevRealE;
            statsData.Rows.InsertAt(myRow, binNumber);
            
            DataRow myRow2 = statsData.NewRow();
            myRow2[0] = "";
            myRow2[1] = "Imaginary";
            myRow2[2] = bin.AverageImagAlpha;
            myRow2[3] = bin.StdDevImagAlpha;
            myRow2[4] = bin.AverageImagE;
            myRow2[5] = bin.StdDevImagE;
            statsData.Rows.InsertAt(myRow2, binNumber + 1);
        }
        //calculates standard deviation of a list
        private double calculateStandardDeviation(List<double> list)
        {
            double stdDev=0, sum=0;
            double average = list.Average();
            foreach (double value in list)
            {
                sum += Math.Pow((value - average), 2);
            }
            if (list.ToArray().Length > 1)
                stdDev = sum / (list.ToArray().Length - 1);
            else
                stdDev = 0;
            stdDev = Math.Sqrt(stdDev);            
            return stdDev;
        }
        //generates final output excel file, with name and list of bin numbers to include
        private void generateOutputFile(string outputFileName, params int[] binNumbers)
        {
            outputGenerator = new ExcelCreator(outputFileName, pathForTemplateOutput, templateOutputName);
            outputGenerator.UpdateValue("Collected Data", "A2", outputFileName, 0, true);
            outputGenerator.UpdateValue("Collected Data", "A3", DateTime.Now.ToShortDateString(), 0, true);
            outputGenerator.UpdateValue("Collected Data", "A4", graphData.Title, 0, true);
            int rowNumber = 7;
            int statsRowNumber = 3;
            for (int i = 0; i < binNumbers.Length; i++)
            {
                outputGenerator.UpdateValue("Collected Data", String.Format("A{0}", rowNumber), bins[binNumbers[i]].BinName, 0, true);
                rowNumber++;
                for (int j = 0; j < bins[binNumbers[i]].binData.ToArray().Length; j++)
                {
                    outputGenerator.UpdateValue("Collected Data", String.Format("A{0}", rowNumber), String.Format("Point {0}", j + 1), 0, true);
                    outputGenerator.UpdateValue("Collected Data", String.Format("B{0}", rowNumber), bins[binNumbers[i]].binData[j].PolynomialOrder.ToString(), 0, false);
                    outputGenerator.UpdateValue("Collected Data", String.Format("C{0}", rowNumber), bins[binNumbers[i]].binData[j].RootUsed1.ToString(), 0, false);
                    outputGenerator.UpdateValue("Collected Data", String.Format("D{0}", rowNumber), bins[binNumbers[i]].binData[j].RootUsed2.ToString(), 0, false);
                    outputGenerator.UpdateValue("Collected Data", String.Format("E{0}", rowNumber), bins[binNumbers[i]].binData[j].RealAlpha.ToString(), 0, false);
                    outputGenerator.UpdateValue("Collected Data", String.Format("F{0}", rowNumber), bins[binNumbers[i]].binData[j].ImagAlpha.ToString(), 0, false);
                    outputGenerator.UpdateValue("Collected Data", String.Format("G{0}", rowNumber), bins[binNumbers[i]].binData[j].RealE.ToString(), 0, false);
                    outputGenerator.UpdateValue("Collected Data", String.Format("H{0}", rowNumber), bins[binNumbers[i]].binData[j].ImagE.ToString(), 0, false);
                    outputGenerator.UpdateValue("Collected Data", String.Format("I{0}", rowNumber), (bins[binNumbers[i]].binData[j].SetUsed+1).ToString(), 0, false);
                    rowNumber++;
                }
                outputGenerator.UpdateValue("Stats", String.Format("A{0}", statsRowNumber), bins[binNumbers[i]].BinName, 0, true);
                outputGenerator.UpdateValue("Stats", String.Format("B{0}", statsRowNumber), bins[binNumbers[i]].AverageRealAlpha.ToString(), 0, false);
                outputGenerator.UpdateValue("Stats", String.Format("C{0}", statsRowNumber), bins[binNumbers[i]].AverageImagAlpha.ToString(), 0, false);
                outputGenerator.UpdateValue("Stats", String.Format("D{0}", statsRowNumber), bins[binNumbers[i]].StdDevRealAlpha.ToString(), 0, false);
                outputGenerator.UpdateValue("Stats", String.Format("E{0}", statsRowNumber), bins[binNumbers[i]].StdDevImagAlpha.ToString(), 0, false);
                outputGenerator.UpdateValue("Stats", String.Format("G{0}", statsRowNumber), bins[binNumbers[i]].AverageRealE.ToString(), 0, false);
                outputGenerator.UpdateValue("Stats", String.Format("H{0}", statsRowNumber), bins[binNumbers[i]].AverageImagE.ToString(), 0, false);
                outputGenerator.UpdateValue("Stats", String.Format("I{0}", statsRowNumber), bins[binNumbers[i]].StdDevRealE.ToString(), 0, false);
                outputGenerator.UpdateValue("Stats", String.Format("J{0}", statsRowNumber), bins[binNumbers[i]].StdDevImagE.ToString(), 0, false);
                statsRowNumber++;
            }
            outputGenerator.closeFile();
        }
        //saves bin data to a text file to be used later
        private void saveBinData(string binFileName)
        {
            StreamWriter dataSaver = new StreamWriter(binFileName);
            dataSaver.WriteLine(bins.Count);//writes the number of bins
            foreach (Bin bin in bins)
            {
                dataSaver.WriteLine(bin.binData.Count);//writes th amount of saved data per bin
                dataSaver.WriteLine(bin.BinName);
                dataSaver.WriteLine(bin.AverageImagAlpha);
                dataSaver.WriteLine(bin.AverageImagE);
                dataSaver.WriteLine(bin.AverageRealAlpha);
                dataSaver.WriteLine(bin.AverageRealE);
                dataSaver.WriteLine(bin.StdDevImagAlpha);
                dataSaver.WriteLine(bin.StdDevImagE);
                dataSaver.WriteLine(bin.StdDevRealAlpha);
                dataSaver.WriteLine(bin.StdDevRealE);
                foreach (MinSearchData data in bin.binData)
                {
                    dataSaver.WriteLine(data.DerivativeValue);
                    dataSaver.WriteLine(data.ImagAlpha);
                    dataSaver.WriteLine(data.ImagE);
                    dataSaver.WriteLine(data.PolynomialOrder);
                    dataSaver.WriteLine(data.RealAlpha);
                    dataSaver.WriteLine(data.RealE);
                    dataSaver.WriteLine(data.RootUsed1);
                    dataSaver.WriteLine(data.RootUsed2);
                    dataSaver.WriteLine(data.SetUsed);
                }
            }
            dataSaver.Close();
        }
        //loads bin data from a bin data file
        private void openBinData(string binFileName)
        {
            StreamReader dataReader = new StreamReader(binFileName);
            int bincount = int.Parse(dataReader.ReadLine());
            displayData.Clear();
            for (int i = 0; i < bincount; i++)
            {
                bins.Add(new Bin());
                int dataCount = int.Parse(dataReader.ReadLine());
                bins[i].BinName = dataReader.ReadLine();
                bins[i].AverageImagAlpha = double.Parse(dataReader.ReadLine());
                bins[i].AverageImagE = double.Parse(dataReader.ReadLine());
                bins[i].AverageRealAlpha = double.Parse(dataReader.ReadLine());
                bins[i].AverageRealE = double.Parse(dataReader.ReadLine());
                bins[i].StdDevImagAlpha = double.Parse(dataReader.ReadLine());
                bins[i].StdDevImagE = double.Parse(dataReader.ReadLine());
                bins[i].StdDevRealAlpha = double.Parse(dataReader.ReadLine());
                bins[i].StdDevRealE = double.Parse(dataReader.ReadLine());
                for (int j = 0; j < dataCount; j++)
                {
                    MinSearchData temp = new MinSearchData();
                    temp.DerivativeValue = double.Parse(dataReader.ReadLine());
                    temp.ImagAlpha = double.Parse(dataReader.ReadLine());
                    temp.ImagE = double.Parse(dataReader.ReadLine());
                    temp.PolynomialOrder = int.Parse(dataReader.ReadLine());
                    temp.RealAlpha = double.Parse(dataReader.ReadLine());
                    temp.RealE = double.Parse(dataReader.ReadLine());
                    temp.RootUsed1 = int.Parse(dataReader.ReadLine());
                    temp.RootUsed2 = int.Parse(dataReader.ReadLine());
                    temp.SetUsed = int.Parse(dataReader.ReadLine());
                    bins[i].binData.Add(temp);
                    if (j == 0)
                        displayData.Rows.Add(bins[i].BinName, bins[i].binData.Count, temp.RealAlpha, temp.ImagAlpha, temp.RealE,
                            temp.ImagE);
                    else
                        displayData.Rows.Add("", bins[i].binData.Count, temp.RealAlpha, temp.ImagAlpha, temp.RealE,
                            temp.ImagE);
                }
            }
            updateBinAveragesAndStdDev();
        }
        //combines a list of bins
        private void combineBins(params int[] binsToCombineNumbers)
        {
            Bin combinedBin = new Bin();
            string name = "";
            for (int i = 0; i < binsToCombineNumbers.Length;i++ )
            {
                foreach (MinSearchData data in bins[binsToCombineNumbers[i]].binData)
                {
                    combinedBin.binData.Add(data);
                }
            }
            bins.Add(combinedBin);
            updateStats(bins.Count - 1);
            for (int i = 0; i < binsToCombineNumbers.Length - 1; i++)
            {
                name += String.Format("{0} and ", bins[binsToCombineNumbers[i]].BinName);
            }
            name += String.Format("{0} combined", bins[binsToCombineNumbers[binsToCombineNumbers.Length - 1]].BinName);
            combinedBin.BinName = name;
            bins.Add(combinedBin);
            binNames.Add(bins.Count-1, name);
            updateBinAveragesAndStdDev();
        }
        //delete an entire bin and updates the display accordingly
        //connected to interface because it expects to act on a grid object
        private void deleteBin(int binToDelete)
        {
            int numRowsToRemove = bins[binToDelete].binData.Count;//keeps track of how many rows of the display should be removed
            bins.RemoveAt(binToDelete);
            if (!exited)
                interfaceChanger.SelectTab("mainProgramPage");
            else
                interfaceChanger.SelectTab("chooseBinsPage");
            //update the combo box information
            binNames.Remove(binToDelete);
            //update other keys in dictionary
            Dictionary<int, string> temp = new Dictionary<int, string>();
            int j = 0;
            for (int i = 0; i < binToDelete; i++)
            {//gets the items that come before the selected bin
                temp.Add(i, bins[i].BinName);
                j++;
            }
            for (int i = binToDelete + 1; i <= binNames.Count; i++)//starts after the index so that it only looks at the right keys and go <= because the dictionary was just decreased so sount is off
            {//creates a temp dictionary with the correnct values
                temp.Add(j, binNames[i]);
                j++;
            }
            binNames = temp;
            this.binOptionsComboBox.DataSource = new BindingSource(binNames, null);
            this.binOptionsComboBox.DisplayMember = "Value";
            this.binOptionsComboBox.ValueMember = "Key";
            this.binsToDeleteComboBox.DataSource = new BindingSource(binNames, null);
            this.binsToDeleteComboBox.DisplayMember = "Value";
            this.binsToDeleteComboBox.ValueMember = "Key";
            //updates stats display
            updateBinAveragesAndStdDev();
            //update data display
            int startPoint = 0;//finds where to start removing rows
            for (int i = 0; i < binToDelete; i++)
            {
                foreach (MinSearchData data in bins[i].binData)
                {
                    startPoint++;
                }
            }
            for (int i = 0; i < numRowsToRemove; i++)
            {
                displayData.Rows.RemoveAt(startPoint + i);//remove all bin rows from data dispay
                startPoint--;//reduce each time because the rows are decreasing so the index to remove at decreases
            }
        }
        //checks to see if two bins have statistically different averages
        //returns true if they are different and false if they are the same
        private bool compareAverages(double avg1, double avg2, double stdDev1, double stdDev2, int n1, int n2)
        {
            bool different = true;
            int degreesOfFreedom = n1 + n2 - 2;
            if (degreesOfFreedom == 0)
            {//if they both are only one value and are different then they are different
                if (avg1 == avg2)
                    return false;
                else
                    return true;
            }
            //calculate t with formula for statistical testing purposes
            //uses formula for t for comparing the meas of two samples
            double sTop = Math.Pow(stdDev1, 2) * (n1 - 1) + Math.Pow(stdDev2, 2) * (n2 - 1);
            double sBottom = n1 + n2 - 2;
            double s = Math.Sqrt(sTop / sBottom);
            double leftT = Math.Abs(avg1 - avg2) / s;
            double rightT = Math.Sqrt((n1 * n2) / (n1 + n2));
            double t = leftT * rightT;

            if (degreesOfFreedom == 1)
            {
                if (t < 12.706)
                    different = false;
            }
            else if (degreesOfFreedom == 2)
            {
                if (t < 4.303)
                    different = false;
            }
            else if (degreesOfFreedom == 3)
            {
                if (t < 3.182)
                    different = false;
            }
            else if (degreesOfFreedom == 4)
            {
                if (t < 2.776)
                    different = false;
            }
            else if (degreesOfFreedom >= 5 && degreesOfFreedom <= 20)
            {//just combining these values because they are slowly going down to 2
                if (t < 2.5)
                    different = false;
            }
            else
            {
                if (t < 2)
                    different = false;
            }
            return different;
        }
        //kills all threads with gnuplot running
        private void killThreads()
        {
            foreach (Thread thread in threadsRunning)
            {
                thread.Interrupt();
            }
        }
        //deletes text and input and output files and folders generated during the program
        private void deleteProgramFiles()
        {
            string mainFolderLocation = programFolderPath + "Generated Files";
            foreach (string fileName in Directory.GetFiles(mainFolderLocation))
            {
                File.Delete(fileName);
            }
            mainFolderLocation += "\\MouseData";
            foreach (string mouseFolderName in Directory.GetDirectories(mainFolderLocation))
            {
                foreach (string file in Directory.GetFiles(mouseFolderName))
                {
                    File.Delete(file);
                }
                Directory.Delete(mouseFolderName);
            }
        }
        //used to update the bin display when adding a value to a pre-existing bin
        private void updateBinDisplay(int binNumber)
        {
            try
            {
                DataRow row = displayData.NewRow();//create row to insert into data
                row[0] = "";
                row[1] = bins[binNumber].binData.Count;
                row[2] = recentSearchData.RealAlpha;
                row[3] = recentSearchData.ImagAlpha;
                row[4] = recentSearchData.RealE;
                row[5] = recentSearchData.ImagE;
                //find where to put it
                int dataLocation = 0;
                for (int i = 0; i <= binNumber; i++)//<= because it needs to go up to and include the chosen bin
                {
                    foreach (MinSearchData data in bins[i].binData)
                    {
                        dataLocation++;
                    }
                    if (i == binNumber)
                        dataLocation--;//take into account that this new value already was added to the bin
                }
                displayData.Rows.InsertAt(row, dataLocation);//now insert it in
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        //used to send bin data to files that can be graphed by gnuplot
        private void createBinGraphs()
        {
            StreamWriter binEGraphCreator = new StreamWriter(binGraphEnergyDataLocation);
            StreamWriter binAGraphCreator = new StreamWriter(binGraphAlphaDataLocation);
            int binNum = 0;
            foreach (Bin b in bins)
            {
                foreach (MinSearchData data in b.binData)
                {
                    binEGraphCreator.Write(String.Format("{0} {1} {2}", data.RealE, data.ImagE, binNum));
                    binAGraphCreator.Write(String.Format("{0} {1} {2}", data.RealAlpha, data.ImagAlpha, binNum));
                    binEGraphCreator.WriteLine();
                    binAGraphCreator.WriteLine();
                }
                binNum++;
            }
            binEGraphCreator.Close();
            binAGraphCreator.Close();
            binGraphA = getBinGraphImage(BGAgnu, "alpha", graphData.StartIRange, graphData.EndIRange, graphData.StartRRange, graphData.EndRRange);
            binGraphE = getBinGraphImage(BGEgnu, "energy", graphData.StartIRange, graphData.EndIRange, graphData.StartRRange, graphData.EndRRange);
            Console.WriteLine("HI");
            this.aBinGraphPictureBox.Image = binGraphA;
            this.eBinGraphPictureBox.Image = binGraphE;
        }

        //need a way to stop the colors from changing when making more bins
        //perhaps rethink the way the colors are generated, might be other options then splot
        //also need to get the range working a bit more properly so you can see things in a useful way
        //need to be able to identify which color goes to which bin
        //some kind of key pre made on gui or part of gnuplot graph
        private Image getBinGraphImage(string dataLocation,string graphType,float startImagRange, float endImagRange, float startRealRange, float endRealRange)
        {
            string fileName = gnuplotLocation;
            Image graph;
            Process gnuplotProcess = new Process();
            gnuplotProcess.StartInfo.FileName = fileName;
            gnuplotProcess.StartInfo.UseShellExecute = false;
            gnuplotProcess.StartInfo.RedirectStandardInput = true;
            gnuplotProcess.StartInfo.RedirectStandardOutput = true;//used to get image
            gnuplotProcess.StartInfo.CreateNoWindow = true;//suppresses command window
            gnuplotProcess.Start();
            StreamWriter gnuplotInput = gnuplotProcess.StandardInput;
            StreamReader gnuplotOutput = gnuplotProcess.StandardOutput;//used to get image
            gnuplotInput.WriteLine(String.Format("set terminal pngcairo size 500,400; set xlabel \"real {0}\"; set ylabel \"imaginary {0}\"; set title\"{0} Bin Values\"",graphType));//used to get image
            gnuplotInput.Flush();
            //gnuplotInput.WriteLine(String.Format("set view map; set palette rgbformulae 33,13,10; splot {0} with points pt 11 ps 5 palette notitle;exit;", dataLocation));
            string plotCommand = "";
            plotCommand = String.Format("plot {0} u 1:2:3 with points ps 2 lc variable notitle; exit", dataLocation);
            gnuplotInput.WriteLine(String.Format("set xrange [{0}:{1}]; set yrange [{2}:{3}];",startRealRange,endRealRange,startImagRange,endImagRange));
            gnuplotInput.Flush();
            gnuplotInput.WriteLine(plotCommand);
            Console.WriteLine(plotCommand);
            graph = Image.FromStream(gnuplotOutput.BaseStream);//used to get image
            gnuplotInput.Close();
            gnuplotProcess.Close();
            Console.WriteLine("here");            
            return graph;
        }
        //save bin graphs
        private void saveBinGraphs()
        {
            //save alpha graph
            saveFileDialog1.ShowDialog();
            string alphaGraph = saveFileDialog1.FileName;
            if (!alphaGraph.EndsWith(".png"))
            {
                alphaGraph += "alphaGraph.png";
            }
            else
            {
                alphaGraph.Insert(-4, "alphaGraph");
            }
            Bitmap alphaBmp = new Bitmap(binGraphA);
            alphaBmp.Save(alphaGraph);
            //save energy graph
            string energyGraph = saveFileDialog1.FileName;
            if (!energyGraph.EndsWith(".png"))
            {
                energyGraph += "energyGraph.png";
            }
            else
            {
                energyGraph.Insert(-4, "energyGraph");
            }
            Bitmap energyBmp = new Bitmap(binGraphE);
            energyBmp.Save(energyGraph);
        }
        #endregion

        //functions that need to be run in threads or background workers
        #region threadBasedFunctions
        //just open up gnuplot window and constantly wait for mouse clicks
        //should be run in a thread so that other things can happen while this is running
        //has to be given the file name of the surface plot data
        //and the data location for that thread
        //graph number variable is used to properly place the graph on the screen
        //polynumberoftotal is the poly number in reference to the total available
        //surfaceFileDataLocation is where windows looks for the data
        [DllImport("user32.dll", EntryPoint = "GetWindowText", ExactSpelling = false, CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpWindowText, int nMaxCount);

        //surfacePlotValues is used for gnuplot graphing, so the file name with extra backslashes in the file location
        //surfaceDataFileLocation is used in creating gridgen data so file location without extra stuff
        //data location is the set the data is in, important for choosing the right data
        //graphNumber is used to keep track of total number of graphs it is on, taking into consideration multiple polynomials
        //polyValue is used mainly in file and graph naming
        //polyNumberOfTotal is to get the number of poly in reference to total number used mainly for row calculations
        private void runGnuplotClicks(string surfacePlotValues, string surfaceDataFileLocation, int dataLocation, int graphNumber, int polyValue = 0, int polyNumberOfTotal = 1)
        {
            double x = 0, y = 0;
            GraphPoint pointChosen = new GraphPoint(0, 0);
            createSurfaceGraph(surfaceDataFileLocation, "gridgen" + polyNumberOfTotal + dataLocation + ".inp");
            string programName = gnuplotLocation;
            Process gnuPlot = new Process();
            gnuPlot.StartInfo.FileName = programName;
            gnuPlot.StartInfo.UseShellExecute = false;
            gnuPlot.StartInfo.RedirectStandardInput = true;
            gnuPlot.StartInfo.CreateNoWindow = true;
            gnuPlot.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            gnuPlot.Start();

            StreamWriter gnuPlotInput = gnuPlot.StandardInput;
            //increases number shown by one to reduce confusion to the user
            //check if no polynomial value was given
            gnuPlotInput.WriteLine(String.Format("set term wxt title '{0}';set pm3d map", polyValue == 0 ? 
                ("s " + (dataLocation + 1)) : ("p " + polyValue + " s " + (dataLocation + 1))));

            gnuPlotInput.Flush();
            //sets the range for the graphs
            gnuPlotInput.WriteLine(String.Format("set xrange [{0}:{1}]; set yrange [{2}:{3}]", graphData.RealGuess - graphData.RealRange,
                graphData.RealGuess + graphData.RealRange, graphData.ImaginaryGuess - graphData.ImaginaryRange,
                graphData.ImaginaryGuess + graphData.ImaginaryRange));
            gnuPlotInput.Flush();
            gnuPlotInput.WriteLine(String.Format("splot {0}", surfacePlotValues + "\" notitle"));
            gnuPlotInput.Flush();
            Thread.Sleep(250);
            //checks to see if there is a second screen attached
            int newGraphLocationX, newGraphLocationY, windowWidth, windowHeight;
            if (Screen.AllScreens.Length > 1)
            {
                Console.WriteLine("HI");
                int screenNumber = 1;
                //calculates the width of the windows based on the number that appear
                //adds an extra graph in the calculations to take into consideration pixels that suck and stuff
                windowWidth = Screen.AllScreens[screenNumber].Bounds.Width / (numberOfGraphs + 2);
                if (windowWidth < 200)
                {
                    windowWidth = 200;
                }
                if (windowWidth > 250)
                {
                    windowWidth = 250;
                }
                
                int numberOfGraphsPerRow = Screen.AllScreens[screenNumber].Bounds.Width / windowWidth;
                //needs to be plus one to adjust for there being only 2
                int numberOfRows = (numberOfGraphs / numberOfGraphsPerRow) + 1;
                windowHeight = Screen.AllScreens[screenNumber].Bounds.Height / (graphData.polynomials.Count * numberOfRows);
                if (windowHeight < 200)
                    windowHeight = 200;
                if (windowHeight > 250)
                    windowHeight = 250;

                int currentRow = (graphNumber / numberOfGraphsPerRow);
                newGraphLocationY = Screen.AllScreens[screenNumber].Bounds.Y + (windowHeight * currentRow);
                int currentColumn = graphNumber - (numberOfGraphsPerRow * currentRow);
                Console.WriteLine(currentColumn.ToString());
                //adjusts the x location by finding which graph it is in the row by taking the graph number and subtracting the number per row
                newGraphLocationX = Screen.AllScreens[screenNumber].Bounds.X + (currentColumn * windowWidth);
                Console.Write("this");
                Console.WriteLine(Screen.AllScreens[screenNumber].Bounds.X.ToString());
            }
            else//math if there is no second screen and everything has to squeeze next to the interface
            {
                int spaceAvailable = Screen.PrimaryScreen.Bounds.Width - this.Width;
                windowWidth = 150;
                int graphsPerRow = spaceAvailable / windowWidth;
                int numberOfRows = (numberOfGraphs / graphsPerRow) + 1;//calculates the number of rows necessary for each polynomial
                windowHeight = Screen.AllScreens[0].Bounds.Height / (graphData.polynomials.Count * numberOfRows);
                if (windowHeight < 150)
                    windowHeight = 150;
                if (windowHeight > 200)
                    windowHeight = 200;
                int currentRow = (graphNumber / graphsPerRow);
                Console.WriteLine(graphNumber.ToString());
                Console.WriteLine(graphsPerRow.ToString());
                Console.WriteLine(windowWidth.ToString());
                int currentColumn = graphNumber - (graphsPerRow * currentRow);
                newGraphLocationX = this.Width + (currentColumn * windowWidth);
                newGraphLocationY = (windowHeight * currentRow);
            }

            Thread.Sleep(20000);
            IntPtr windowId = IntPtr.Zero;
            while (windowId == IntPtr.Zero)//keeps trying to get the id until it has it
                windowId = FindWindowByCaption(IntPtr.Zero, "p " + polyValue + " s " + (dataLocation + 1));

            MoveWindow(windowId, newGraphLocationX, newGraphLocationY, windowWidth, windowHeight, true);

            FileSystemWatcher mouseDataChecker = new FileSystemWatcher();
            try
            {
                //adds on an extra folder so that each file will be in their own folder
                if (!Directory.Exists(mouseDataLocation + "mouseData" + dataLocation+polyValue))
                {
                    Directory.CreateDirectory(mouseDataLocation + "mouseData" + dataLocation+polyValue);
                }
                mouseDataChecker.Path = mouseDataLocation + "mouseData" + dataLocation + polyValue;
                mouseDataChecker.Filter = String.Format("mouseData{0}.txt", dataLocation.ToString() + polyValue.ToString());
            }
            catch
            {
                MessageBox.Show("Mouse data file info incorrect");
            }
            try
            {
                while (true)
                {
                    var oldPosition = GetPlacement(windowId);
                    
                    //adds on an extra folder to the name so that each will be in their own folder
                    gnuPlotInput.WriteLine("pause mouse; set print {0}; print MOUSE_X, MOUSE_Y; unset print;", mouseDataFile + "mouseData" + dataLocation + polyValue +
                        "\\\\" + "mouseData" + dataLocation + polyValue + ".txt\"");
                    //waits for user to choose a point before resetting the gnuplot mouse clicks
                    WaitForChangedResult changeResult = mouseDataChecker.WaitForChanged(WatcherChangeTypes.All);
                    gnuPlotInput.Flush();
                    if (oldPosition.showCmd != GetPlacement(windowId).showCmd)//if the window was maximized it will go to normal
                    {
                        ShowWindow(windowId, 1);//move window back to normal
                    }
                    Thread.Sleep(250);
                    //opens up file and gets mouse data
                    string data = "";
                    try
                    {
                        StreamReader mouseDataReader = new StreamReader(String.Format("{0}mouseData{1}\\mouseData{1}.txt", mouseDataLocation, (dataLocation.ToString() + polyValue.ToString())));
                        data = mouseDataReader.ReadToEnd();
                        mouseDataReader.Close();
                    }
                    catch
                    {
                        Console.WriteLine("mouse file does not exit");
                    }                   
                    string[] dataPoints = data.Split(' ');
                    try
                    {
                        x = double.Parse(dataPoints[0]);
                        y = double.Parse(dataPoints[1]);
                    }
                    catch
                    {
                        MessageBox.Show("mouse data failed to read properly");
                    }
                    pointChosen.X = x;
                    pointChosen.Y = y;
                    mouseDataLabel.Text = String.Format("Point Chosen: ({0}, {1})", pointChosen.X, pointChosen.Y);
                    graphData.Polynomial = polyValue;//updates the polynomial value to be used in the min searcher program
                    searchForMin(pointChosen.X, pointChosen.Y, dataLocation, "gridgen" + (dataLocation + polyValue) + ".inp");
                    this.derivativeDataLabel.Text = String.Format("Derivative: {0}", recentSearchData.DerivativeValue);
                    this.stationaryEnergyLabel.Text = String.Format("Stat E: ({0}, {1})", recentSearchData.RealE, recentSearchData.ImagE);
                    this.stationaryPointLabel.Text = String.Format("Stat Pt: ({0}, {1})", recentSearchData.RealAlpha, recentSearchData.ImagAlpha);
                    if (recentSearchData.DerivativeValue > 1E-15)
                    {
                        changeLabelColorFromThread(derivativeDataLabel, System.Drawing.Color.Red);
                        switchLabelFromThread(derivativeWarningLabel, true);
                    }
                    else
                    {
                        changeLabelColorFromThread(derivativeDataLabel, System.Drawing.Color.Black);
                        switchLabelFromThread(derivativeWarningLabel, false);
                    }
                    if (recentSearchData.RealAlpha < minX || recentSearchData.RealAlpha > maxX)
                    {
                        changeLabelColorFromThread(stationaryPointLabel, System.Drawing.Color.Red);
                        switchLabelFromThread(statPtWarningLabel, true);
                    }
                    else
                    {
                        changeLabelColorFromThread(stationaryPointLabel, System.Drawing.Color.Black);
                        switchLabelFromThread(statPtWarningLabel, false);
                    }
                    //turn on labels and buttons using seperate functions
                    switchLabelFromThread(derivativeDataLabel, true);
                    switchLabelFromThread(stationaryPointLabel, true);
                    switchLabelFromThread(stationaryEnergyLabel, true);
                    switchLabelFromThread(mouseDataLabel, true);
                    //only brings up the regular save features if autosave is off
                    if (!autoSave)
                    {
                        switchButtonFromThread(clearMinDataButton, true);
                        switchButtonFromThread(saveMinDataButton, true);
                    }
                    else
                    {
                        saveData(autoSaveBin, recentSearchData);
                        Console.WriteLine("HI");
                        this.dataGridView1.BeginInvoke(new MethodInvoker(() =>
                        {
                            updateBinDisplay(autoSaveBin);
                        }
                        ));
                        Console.WriteLine("BYE");
                        dataGridView1.Refresh();
                        statsDataGridView.Refresh();
                    }
                    this.Refresh();
                    Console.WriteLine("what up");
                }
            }
            catch (ThreadInterruptedException ex)
            {
                gnuPlot.Kill();
                gnuPlotInput.Close();
                mouseDataChecker.Dispose();
            }
        }

        public static void changeLabelColorFromThread(Label label, System.Drawing.Color color)
        {
            if (label.InvokeRequired)
            {
                label.Invoke((MethodInvoker)(() => label.ForeColor = color));
            }
            else
            {
                label.ForeColor = color;
            }
        }

        public static void switchLabelFromThread(Label label, bool offOrOn)
        {
            if (label.InvokeRequired)
            {
                label.Invoke((MethodInvoker)(()=>label.Visible=offOrOn));
            }
            else
            {
                label.Visible = true;
            }
        }

        public static void switchButtonFromThread(Button button, bool offOrOn)
        {
            if (button.InvokeRequired)
            {
                button.Invoke((MethodInvoker)(() => button.Visible = offOrOn));
            }
            else
            {
                button.Visible = true;
            }
        }
        #endregion

        //Functions for button clicks
        #region buttonClicks
        //buttons associated with the beginning of the program from opening files to choosing data
        #region beginningOfProgramButtons
        private void openFileButton_Click(object sender, EventArgs e)
        {
            this.openFileDialog1.ShowDialog();
            try
            {
                readInitialData(this.openFileDialog1.FileName);
                createScatterPlot(true);
                minX = totalData[0].data[0].XValue;
                this.startOfRangeTextBox.Text = minX.ToString();
                maxX = totalData[NumDataPoints - 1].data[0].XValue;
                this.endOfRangeTextBox.Text = maxX.ToString();
                interfaceChanger.SelectTab("chooseInitialDataPage");
                graphData.ProgramType = "grid";
                graphData.GridSize = 1000;
                masterDataLocation = 0;
                this.graphPictureBox1.Visible = true;
                this.finishProgramButton.Visible = true;
                //now turns off unnecessary things
                this.chooseMinRootsButton.Visible = false;
                this.openFileButton.Visible = false;
                this.loadBinDataButton.Visible = false;
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
                if (this.openFileDialog1.FileName != "")
                    MessageBox.Show("Input file not in correct format");
            }
        }
        private void loadBinDataButton_Click(object sender, EventArgs e)
        {
            bins.Clear();
            binNames.Clear();
            openFileDialog1.ShowDialog();
            try
            {
                openBinData(openFileDialog1.FileName);
                for (int i = 0; i < bins.Count; i++)
                {
                    binNames.Add(i, bins[i].BinName);//puts bin names in dropDown box for selecting bin to save data in
                }
                //sets up combo boxes
                this.binOptionsComboBox.DataSource = new BindingSource(binNames, null);
                this.binOptionsComboBox.DisplayMember = "Value";
                this.binOptionsComboBox.ValueMember = "Key";
                this.binsToDeleteComboBox.DataSource = new BindingSource(binNames, null);
                this.binsToDeleteComboBox.DisplayMember = "Value";
                this.binsToDeleteComboBox.ValueMember = "Key";
                this.autoSaveBinComboBox.DataSource = new BindingSource(binNames, null);
                this.autoSaveBinComboBox.DisplayMember = "Value";
                this.autoSaveBinComboBox.ValueMember = "Key";

                this.dataGridView1.Visible = true;
                this.statsDataGridView.Visible = true;
                this.savedDataLabel.Visible = true;
                this.statsDataLabel.Visible = true;
                MessageBox.Show("Data loaded, Please choose input file");
            }
            catch
            {
                MessageBox.Show("Bin data failed to read from file");
            }
        }
        private void chooseDataButton_Click(object sender, EventArgs e)
        {
            double startRange = 0, endRange = 0;
            List<int> roots = new List<int>();
            try
            {
                startRange = double.Parse(this.startOfRangeTextBox.Text);
            }
            catch
            {
                MessageBox.Show("Start of x Range invalid");
                this.startOfRangeTextBox.Text = "";
                return;
            }
            try
            {
                endRange = double.Parse(this.endOfRangeTextBox.Text);
            }
            catch
            {
                MessageBox.Show("End of x Range invalid");
                this.endOfRangeTextBox.Text = "";
                return;
            }
            string[] parts = this.rootsToIncludeTextBox.Text.Split(' ');
            int temp = 0;
            if (parts.Length > 5)
            {
                MessageBox.Show("No more than 5 roots");
                this.rootsToIncludeTextBox.Text = "";
                return;
            }
            if (parts.Length == 3 && parts[1] == "-")//if they want to put in a range
            {
                int startPoint, endPoint;
                bool start = true;
                try
                {
                    startPoint = int.Parse(parts[0]);
                    if (startPoint > NumRoots)
                        throw new Exception();
                    start = false;//identifies that it is working on the end value
                    endPoint = int.Parse(parts[2]);
                    if (endPoint > NumRoots || endPoint < startPoint)
                        throw new Exception();
                    for (int i = startPoint; i <= endPoint; i++)
                    {
                        roots.Add(i);
                    }
                }
                catch
                {
                    MessageBox.Show(String.Format("{0} of root range invalid", start ? "start" : "end"));
                    return;
                }
            }
            else if (parts.Length == 1 && !int.TryParse(parts[0], out temp))//they are doing a range but forgot spaces
            {
                bool start = true;
                int startPoint, endPoint;
                parts = this.rootsToIncludeTextBox.Text.Split('-');//now split by the hyphen
                try
                {
                    startPoint = int.Parse(parts[0]);
                    if (startPoint > NumRoots)
                        throw new Exception();
                    start = false;//identifies that it is working on the end value
                    endPoint = int.Parse(parts[1]);
                    if (endPoint > NumRoots || endPoint < startPoint)
                        throw new Exception();
                    for (int i = startPoint; i <= endPoint; i++)
                    {
                        roots.Add(i);
                    }
                }
                catch
                {
                    MessageBox.Show(String.Format("{0} of root range invalid", start ? "start" : "end"));
                    return;
                }
            }
            else//if they just put in all the roots they want to include
            {
                int index = 0;
                try
                {
                    for (index = 0; index < parts.Length; index++)
                    {
                        int root = int.Parse(parts[index]);
                        if (root > NumRoots)
                        {
                            throw new Exception();
                        }
                        else
                            roots.Add(root);
                    }
                }
                catch
                {
                    MessageBox.Show(String.Format("Root number {0} is invalid", index + 1));
                    return;
                }
            }
            if (roots.Count > 5)
            {
                MessageBox.Show("You cannot have more than 5 roots");
                return;
            }
            createMasterData(startRange, endRange, roots.ToArray());
            createScatterPlot(false);
            this.graphPictureBox1.Visible = true;
            interfaceChanger.SelectTab("graphVariablesPage");
        }
        private void reChooseDataButton_Click(object sender, EventArgs e)
        {
            createScatterPlot(true);//recreates the scatter plot with the total data
            this.graphPictureBox1.Visible = true;
            interfaceChanger.SelectTab("chooseInitialDataPage");
            masterDataSet.Clear();//empties the master data before it is changed
        }
        #endregion
        //buttons associated with saving or clearing min data gathered by clicking on the graph
        #region saveMinDataButtons
        private void clearMinDataButton_Click(object sender, EventArgs e)
        {
            this.derivativeDataLabel.Visible = false;
            this.stationaryEnergyLabel.Visible = false;
            this.stationaryPointLabel.Visible = false;
            this.clearMinDataButton.Visible = false;
            this.saveMinDataButton.Visible = false;
            this.mouseDataLabel.Visible = false;
            this.derivativeWarningLabel.Visible = false;
            this.statPtWarningLabel.Visible = false;
        }
        private void saveMinDataButton_Click(object sender, EventArgs e)
        {
            interfaceChanger.SelectTab("saveDataPage");
            dataAndGraphChanger.SelectTab("dataPage");
            if (bins.Count == 0)
            {
                this.deleteDataButton.Visible = false;
                this.useExistingBinButton.Visible = false;
            }
            else
            {
                this.deleteDataButton.Visible = true;
                this.useExistingBinButton.Visible = true;
            }
            this.clearMinDataButton.Visible = false;
            this.saveMinDataButton.Visible = false;
            this.mouseDataLabel.Visible = false;
            this.derivativeDataLabel.Visible = false;
            this.stationaryEnergyLabel.Visible = false;
            this.stationaryPointLabel.Visible = false;
            this.derivativeWarningLabel.Visible = false;
            this.statPtWarningLabel.Visible = false;
            if (bins.ToArray().Length != 0)
                this.useExistingBinButton.Visible = true;
            this.createNewBinButton.Visible = true;
            this.changeGraphOrDataButton.Text = "View Scatter Plot";
        }
        private void createNewBinButton_Click(object sender, EventArgs e)
        {
            this.createNewBinButton.Visible = false;
            this.useExistingBinButton.Visible = false;
            this.derivativeDataLabel.Visible = false;
            this.mouseDataLabel.Visible = false;
            this.chooseBinNameLabel.Visible = true;
            this.newBinNameTextBox.Visible = true;
            this.nameNewBinButton.Visible = true;
            this.newBinNameTextBox.Text = "";//gets rid of previous text
            //makes focus be the name text box
            this.ActiveControl = this.newBinNameTextBox;
        }
        private void nameNewBinButton_Click(object sender, EventArgs e)
        {
            if (newBinNameTextBox.Text == "")
            {
                MessageBox.Show("Please enter bin name");
                return;
            }
            else if (binNames.ContainsValue(newBinNameTextBox.Text))
            {
                MessageBox.Show(String.Format("Bin name \"{0}\" already exists", newBinNameTextBox.Text));
                newBinNameTextBox.Text = "";
                return;
            }
            else
            {
                if (newBinNameTextBox.Text == "bunny")
                {
                    this.graphPictureBox1.Image = Properties.Resources.bunny;
                }
                else
                {
                    this.graphPictureBox1.Image = recentScatterPlot;
                }
                //creates a new bin bc it gives the function a bin number one greater than available
                if (bins.Count == 0)
                {
                    this.changeGraphOrDataButton.Visible = true;
                }
                saveData(bins.ToArray().Length, recentSearchData);
                bins[bins.Count - 1].BinName = newBinNameTextBox.Text;
                try
                {
                    binNames.Add(bins.Count - 1, bins[bins.Count - 1].BinName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    MessageBox.Show((bins.Count - 1).ToString());
                }
                this.binOptionsComboBox.DataSource = new BindingSource(binNames, null);
                this.binOptionsComboBox.DisplayMember = "Value";
                this.binOptionsComboBox.ValueMember = "Key";
                this.binsToDeleteComboBox.DataSource = new BindingSource(binNames, null);
                this.binsToDeleteComboBox.DisplayMember = "Value";
                this.binsToDeleteComboBox.ValueMember = "Key";
                this.chooseBinNameLabel.Visible = false;
                this.newBinNameTextBox.Visible = false;
                this.nameNewBinButton.Visible = false;
                interfaceChanger.SelectTab("mainProgramPage");

                displayData.Rows.Add(bins[bins.Count - 1].BinName, bins[bins.Count - 1].binData.Count,
                    recentSearchData.RealAlpha, recentSearchData.ImagAlpha, recentSearchData.RealE, recentSearchData.ImagE);
                //updateBinAveragesAndStdDev();

                dataAndGraphChanger.SelectTab("dataPage");
                this.deleteDataButton.Visible = true;
            }
        }
        //makes enter key work for naming a new bin
        private void newBinNameTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)
            {
                nameNewBinButton_Click(sender, e);
            }
        }
        private void useExistingBinButton_Click(object sender, EventArgs e)
        {
            this.useExistingBinButton.Visible = false;
            this.createNewBinButton.Visible = false;
            this.selectBinButton.Visible = true;
            this.binOptionsComboBox.Visible = true;
            this.chooseBinLabel.Visible = true;

        }
        private void selectBinButton_Click(object sender, EventArgs e)
        {
            int binNumber = 0;
            try
            {
                binNumber = this.binOptionsComboBox.SelectedIndex;
            }
            catch
            {
                MessageBox.Show("Please select a bin");
                return;
            }

            saveData(binNumber,recentSearchData);
            updateBinDisplay(binNumber);
            
            this.binOptionsComboBox.Visible = false;
            this.selectBinButton.Visible = false;
            this.chooseBinLabel.Visible = false;
            this.deleteDataButton.Visible = true;
            interfaceChanger.SelectTab("mainProgramPage");
            dataAndGraphChanger.SelectTab("dataPage");
        }
        private void autoSaveButton_Click(object sender, EventArgs e)
        {
            if (autoSave)
            {
                autoSave = false;
                chooseAutoSaveBin = true;
                this.autoSaveButton.Text = "Turn Auto Save On";
                this.chooseAutoSaveBinLabel.Visible = false;
            }
            else
            {
                if (binNames.Count > 0)
                {
                    //means that they still have to choose the autosave bin and will display options to do so
                    if (chooseAutoSaveBin)
                    {
                        this.chooseAutoSaveBinLabel.Text = "Save To Bin";
                        this.chooseAutoSaveBinLabel.Visible = true;
                        this.autoSaveButton.Text = "Choose Bin";
                        this.autoSaveBinComboBox.Visible = true;
                        this.autoSaveBinComboBox.DataSource = new BindingSource(binNames, null);
                        this.autoSaveBinComboBox.DisplayMember = "Value";
                        this.autoSaveBinComboBox.ValueMember = "Key";
                        chooseAutoSaveBin = false;
                    }
                    else
                    {
                        this.autoSaveBinComboBox.Visible = false;
                        autoSaveBin = this.autoSaveBinComboBox.SelectedIndex;
                        autoSave = true;
                        this.autoSaveButton.Text = "Turn Auto Save Off";
                        this.chooseAutoSaveBinLabel.Text = String.Format("Saving to Bin \"{0}\"", binNames[autoSaveBin]);
                    }
                }
            }
        }
        #endregion
        //buttons associated with the surface graph from graphing it to changing some of the variables
        #region surfaceGraphManipulationButtons
        private void changeGraphButton_Click(object sender, EventArgs e)
        {
            killThreads();
            oldDataLocation = masterDataLocation;
            this.dataSetLabel.Text = String.Format("Data Sets to Show: {0} to {1}", masterDataLocation + 1, masterDataLocation + numberOfGraphs);
            this.graphPictureBox1.Visible = true;
            this.changingDataLabel.Visible = true;
            interfaceChanger.SelectTab("graphVariablesPage");
            dataAndGraphChanger.SelectTab("graphPage");
            //now turns off unnecessary things
            this.chooseMinRootsButton.Visible = false;
            //delete old files
            deleteProgramFiles();

            this.dataSetLabel.Text = String.Format("Data Sets to Show: {0} to {1}", masterDataLocation + 1, masterDataLocation + numberOfGraphs);
            this.realGuessTextBox.Text = graphData.RealGuess.ToString();
            this.imaginaryGuessTextBox.Text = graphData.ImaginaryGuess.ToString();
            this.imaginaryRangeTextBox.Text = graphData.ImaginaryRange.ToString();
            string polynomialsForTextBox = "";
            for (int i = 0; i < graphData.polynomials.Count-1; i++)
            {
                polynomialsForTextBox += graphData.polynomials[i].ToString();
                polynomialsForTextBox += " ";
            }
            polynomialsForTextBox += graphData.polynomials[graphData.polynomials.Count - 1];
            this.polynomialTextBox.Text = polynomialsForTextBox;
            this.realRangeTextBox.Text = graphData.RealRange.ToString();
            this.rootInUse1TextBox.Text = graphData.RootInUse1.ToString();
            this.rootInUse2TextBox.Text = graphData.RootInUse2.ToString();
            this.toleranceTextBox.Text = graphData.Tolerance;
            this.mouseDataLabel.Visible = false;
            this.derivativeDataLabel.Visible = false;
            this.statPtWarningLabel.Visible = false;
            this.derivativeWarningLabel.Visible = false;
            this.stationaryPointLabel.Visible = false;
            this.stationaryEnergyLabel.Visible = false;
            this.saveMinDataButton.Visible = false;
            this.clearMinDataButton.Visible = false;
        }
        //used to get values for initial graph, and any changes later on
        private void regraphButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.imaginaryGuessTextBox.Text != "")
                {//makes it so that it only tries to set initial guess on first graph
                    graphData.ImaginaryGuess = double.Parse(imaginaryGuessTextBox.Text);
                }
            }
            catch
            {
                MessageBox.Show("Improper value for initial imaginary guess");
                return;
            }
            try
            {
                if (this.realGuessTextBox.Text != "")
                {
                    graphData.RealGuess = double.Parse(realGuessTextBox.Text);
                }
            }
            catch
            {
                MessageBox.Show("Improper value for the initial real guess");
                return;
            }
            try
            {
                List<int> polynomials = new List<int>();
                List<int> oldPolynomials = graphData.polynomials;
                int oldPolynomial = graphData.Polynomial;
                int temp = 0;
                int startPoint, endPoint;
                string[] parts = this.polynomialTextBox.Text.Split(' ');
                int maxPolynomials = 5;
            
                //if they put in a range in the form of start - end
                bool start = true;
                try
                {
                    if (parts.Length == 3 && parts[1] == "-")
                    {
                        startPoint = int.Parse(parts[0]);
                        start = false;//identifies that it is working on the end value
                        endPoint = int.Parse(parts[2]);
                        if (endPoint < startPoint)
                            throw new Exception();
                        for (int i = startPoint; i <= endPoint; i++)
                        {
                            polynomials.Add(i);
                        }
                    }
                    else if (parts.Length == 1 && !int.TryParse(parts[0], out temp))//if form is start-end
                    {
                        parts = this.polynomialTextBox.Text.Split('-');//now split by the hyphen
                        startPoint = int.Parse(parts[0]);
                        start = false;//identifies that it is working on the end value
                        endPoint = int.Parse(parts[1]);
                        if (endPoint < startPoint)
                            throw new Exception();
                        for (int i = startPoint; i <= endPoint; i++)
                        {
                            polynomials.Add(i);
                        }
                    }
                    else//they just did them one by one
                    {
                        int index = 0;
                        try
                        {
                            for (index = 0; index < parts.Length; index++)
                            {
                                int poly = int.Parse(parts[index]);
                                polynomials.Add(poly);
                            }
                        }
                        catch
                        {
                            MessageBox.Show(String.Format("Polynomial number {0} is invalid", index + 1));
                            return;
                        }
                    }
                }
                catch
                {
                    MessageBox.Show(String.Format("{0} of Polynomial range invalid", start ? "start" : "end"));
                    return;
                }
                //limit of number of polynomials
                if (polynomials.Count > maxPolynomials)
                {
                    MessageBox.Show("No more than 5 polynomials at once");
                    return;
                }
                //gets rid of old values first
                if (graphData.polynomials.Count != 0)
                    graphData.polynomials.Clear();
                Console.WriteLine(graphData.polynomials.Count);
                for (int i = 0; i < polynomials.Count; i++)
                {
                    graphData.polynomials.Add(polynomials[i]);
                }//puts values in graph data

                for (int i = 0; i < graphData.polynomials.Count; i++)
                {
                    Console.WriteLine(graphData.polynomials[i]);
                }

                //need to decide what to do about changing poly and the set value
            }
            catch(Exception ex)
            {
                MessageBox.Show("Improper value for polynomial");
                Console.WriteLine(ex.Message);
                return;
            }

            try
            {
                graphData.RootInUse1 = int.Parse(this.rootInUse1TextBox.Text);
            }
            catch
            {
                MessageBox.Show("Improper value for root 1");
                return;
            }
            try
            {
                graphData.RootInUse2 = int.Parse(this.rootInUse2TextBox.Text);
            }
            catch
            {
                MessageBox.Show("Improper value for root 2");
                return;
            }
            try
            {
                graphData.RealRange = double.Parse(this.realRangeTextBox.Text);
            }
            catch
            {
                MessageBox.Show("Improper value for real range");
                return;
            }
            try
            {
                graphData.ImaginaryRange = double.Parse(this.imaginaryRangeTextBox.Text);
            }
            catch
            {
                MessageBox.Show("Improper value for imaginary range");
                return;
            }
            if (this.toleranceTextBox.Text != "")
            {
                graphData.Tolerance = toleranceTextBox.Text;
            }
            else
            {
                MessageBox.Show("Improper value for tolerance");
                return;
            }
            //get values for data ranges
            try
            {
                graphData.StartRRange = float.Parse(this.minRealTextBox.Text);
                graphData.EndRRange = float.Parse(this.maxRealTextBox.Text);
                graphData.StartIRange = float.Parse(this.startImagTextBox.Text);
                graphData.EndIRange = float.Parse(this.endImagTextBox.Text);
            }
            catch
            {
                MessageBox.Show("Improper value in one of energy window values");
            }
            try
            {
                numberOfGraphs = int.Parse(this.numberOfGraphsTextBox.Text);
            }
            catch
            {
                MessageBox.Show("Improper value for number of graphs");
                return;
            }
            //I dont want more than ten per polynomial
            if (numberOfGraphs > 10)
            {
                MessageBox.Show("No more then 10 graphs, reducing number to 10");
                numberOfGraphs = 10;
            }
            if (numberOfGraphs * graphData.polynomials.Count > maxGraphsOnScreen)
            {
                MessageBox.Show("Sorry you are trying to create more graphs than your screen can handle");
                return;
            }
        
            
            dataAndGraphChanger.SelectTab("graphPage");
            interfaceChanger.SelectTab("blankPage");
            this.graphPictureBox1.Image = Min_Searcher_2_Prototype_5.Properties.Resources.Generating_Graph_Image;
            surfaceGraphDataNames.Clear();
            threadsRunning.Clear();

            //extra try block
            try
            {
                //number starts at the current masterdatalocation and goes a certain amount afterwards
                //makes it more consistent I hope
                int startPoint = masterDataLocation;
                int endPoint = masterDataLocation + numberOfGraphs;
                for (int j = 0; j < graphData.polynomials.Count; j++)
                {
                    graphData.Polynomial = graphData.polynomials[j];
                    surfaceGraphDataNames.Clear();
                    masterDataLocation = startPoint;//important to reset location when starting to graph next polynomials
                    for (int i = startPoint; i < endPoint; i++)
                    {
                        Console.WriteLine(String.Format("i={0} poly={1}", i, graphData.Polynomial));
                        //this makes a list of just the generic file name without the path to the location
                        surfaceGraphDataNames.Add(String.Format("ThreadData{0}Poly{1}.txt", i, graphData.Polynomial));
                        Thread.Sleep(1000);
                        try
                        {
                            generateInputFile(masterDataLocation, "gridgen" + j + masterDataLocation + ".inp");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Not enough data remaining to create an input file");
                            Console.WriteLine(ex.Message);
                            masterDataLocation = masterDataSet.Count - ((graphData.NumRootsUsed + 1) * (graphData.Polynomial + 1) - 1);
                            this.dataSetLabel.Text = String.Format("Data Sets to Show: {0} to {1}", masterDataLocation + 1, masterDataLocation + numberOfGraphs);
                            return;
                        }
                        try
                        {
                            //values saved so that threads use the proper point
                            int number = (i - startPoint);
                            int threadNumber = number + (j * numberOfGraphs);
                            int threadDataLocation = masterDataLocation;
                            string fileName = surfaceGraphDataNames[number];
                            int polynomialInUse = graphData.Polynomial;
                            int tempJ = j;
                            //this adds on the normal path location to the surface graph data generation function
                            //creates a thread to create gridgen data
                            //Thread dataGenerator = new Thread(() =>
                            //    createSurfaceGraph(surfacePlotDataLocation + surfaceGraphDataNames[number], "gridgen" + masterDataLocation + ".inp"));
                            //dataGenerator.Start();//starts thread
                            threadsRunning.Add(new Thread(() =>
                            {
                                //this appends the gnuplot specific file location for the gnuplot graph generation
                                //passes in i - startPoint because it needs to know where to put the graph on screen
                                //this is important because other wise it won't display right when not starting on a multiple of the number of graphs
                                //gives it j
                                runGnuplotClicks(surfacePlotData + fileName, surfacePlotDataLocation + fileName, threadDataLocation,
                                    number + (tempJ * numberOfGraphs), polynomialInUse, tempJ);
                                //the graph number increased to account for multiple polynomials
                            }
                            ));
                            Console.WriteLine(threadNumber);
                            threadsRunning[threadNumber].Start();
                        }
                        catch (Exception ex)
                        {
                            this.graphPictureBox1.Image = null;//makes image go away if data fails
                            MessageBox.Show("Gridgen failed to generate good data");
                            Console.WriteLine(ex.Message);
                        }
                        masterDataLocation++;
                    }
                }
            }
            catch
            {
                Console.WriteLine("Caught it");
            }


            if (bins.Count > 0)
                dataAndGraphChanger.SelectTab("dataPage");
            this.changeMinRootsButton.Visible = true;
            this.reChooseDataButton.Visible = false;
            interfaceChanger.SelectTab("mainProgramPage");
            if (bins.Count > 0)
            {
                this.dataAndGraphChanger.Visible = true;
            }
            this.changeGraphButton.Visible = true;
            //makes the min roots initially the same as the graphing roots
            MinRoot1 = graphData.RootInUse1;
            MinRoot2 = graphData.RootInUse2;
            //resets default min ranges to .05
            MinRealRange = .05;
            MinImagRange = .05;
            //turn off options for initial guess because it will not change during the process
            this.changingDataLabel.Visible = false;
            this.dataSetLabel.Visible = true;
            this.advanceDataSetButton.Visible = true;
            this.decreaseDataSetButton.Visible = true;
            this.changeGraphOrDataButton.Text = "View Scatter Plot";
            firstGraph = false;
            //generates new scatter plot based off of current working data
            createScatterPlot(false);
        }
        private void advanceDataSetButton_Click(object sender, EventArgs e)
        {
            masterDataLocation++;
            this.dataSetLabel.Text = String.Format("Data Sets to Show: {0} to {1}", masterDataLocation + 1, masterDataLocation + numberOfGraphs);
        }
        private void decreaseDataSetButton_Click(object sender, EventArgs e)
        {
            if(masterDataLocation>0)
                masterDataLocation--;
            this.dataSetLabel.Text = String.Format("Data Sets to Show: {0} to {1}", masterDataLocation + 1, masterDataLocation + numberOfGraphs);
        }
        private void changeMinRootsButton_Click(object sender, EventArgs e)
        {
            interfaceChanger.SelectTab("graphVariablesPage");
            this.chooseMinRootsButton.Visible = true;
            //turn on values
            this.rootsToUseLabel.Visible = true;
            this.rootInUse1TextBox.Visible = true;
            this.rootInUse2TextBox.Visible = true;
            this.realRangeLabel.Visible = true;
            this.realRangeTextBox.Visible = true;
            this.imaginaryRangeLabel.Visible = true;
            this.imaginaryRangeTextBox.Visible = true;
            //turn off extra things
            this.realLabel.Visible = false;
            this.imaginaryLabel.Visible = false;
            this.initialGuessLabel.Visible = false;
            this.realGuessTextBox.Visible = false;
            this.imaginaryGuessTextBox.Visible = false;
            this.polynomialValueLabel.Visible = false;
            this.polynomialTextBox.Visible = false;
            this.toleranceLabel.Visible = false;
            this.toleranceTextBox.Visible = false;
            this.advanceDataSetButton.Visible = false;
            this.decreaseDataSetButton.Visible = false;
            this.regraphButton.Visible = false;
            this.dataSetLabel.Visible = false;
            this.numberOfGraphsLabel.Visible = false;
            this.numberOfGraphsTextBox.Visible = false;
            //sets the range textboxes text to the min version
            this.realRangeTextBox.Text = MinRealRange.ToString();
            this.imaginaryRangeTextBox.Text = MinImagRange.ToString();
        }
        private void chooseMinRootsButton_Click(object sender, EventArgs e)
        {
            try
            {
                MinRoot1 = int.Parse(this.rootInUse1TextBox.Text);
            }
            catch
            {
                MessageBox.Show("Improper value for root 1");
                return;
            }
            try
            {
                MinRoot2 = int.Parse(this.rootInUse2TextBox.Text);
            }
            catch
            {
                MessageBox.Show("Improper value for root 2");
                return;
            }
            try
            {
                MinRealRange = double.Parse(this.realRangeTextBox.Text);
            }
            catch
            {
                MessageBox.Show("Improper value for real range");
                return;
            }
            try
            {
                MinImagRange = double.Parse(this.imaginaryRangeTextBox.Text);
            }
            catch
            {
                MessageBox.Show("Improper value for imaginary range");
                return;
            }
            
            this.changeMinRootsButton.Visible = true;
            //turn things back on
            this.realLabel.Visible = true;
            this.imaginaryLabel.Visible = true;
            this.initialGuessLabel.Visible = true;
            this.realGuessTextBox.Visible = true;
            this.imaginaryGuessTextBox.Visible = true;
            this.polynomialValueLabel.Visible = true;
            this.polynomialTextBox.Visible = true;
            this.toleranceLabel.Visible = true;
            this.toleranceTextBox.Visible = true;
            this.advanceDataSetButton.Visible = true;
            this.decreaseDataSetButton.Visible = true;
            this.regraphButton.Visible = true;
            this.dataSetLabel.Visible = true;
            this.numberOfGraphsLabel.Visible = true;
            this.numberOfGraphsTextBox.Visible = true;

            interfaceChanger.SelectTab("mainProgramPage");
        }
        private void changeGraphOrDataButton_Click(object sender, EventArgs e)
        {
            if (dataAndGraphChanger.SelectedTab == dataAndGraphChanger.TabPages["dataPage"])
            {
                changeGraphOrDataButton.Text = "View Saved Data";
                dataAndGraphChanger.SelectTab("graphPage");
            }
            else
            {
                changeGraphOrDataButton.Text = "View Scatter Plot";
                dataAndGraphChanger.SelectTab("dataPage");
            }
            this.Refresh();//refreshes the page so that the text change will be seen
        }
        #endregion
        //buttons associated with the end of the program, from ending it to anything having to do with
        //generating output files including the functions that run the check boxes
        #region endOfProgramOptionsButtons
        private void finishProgramButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }
        private void generateOutputButton_Click(object sender, EventArgs e)
        {
            if (checkedBins.Count == 0)
            {
                MessageBox.Show("Please select bins to include");
            }
            else
            {
                this.saveFileDialog1.ShowDialog();
                generateOutputFile(saveFileDialog1.FileName, checkedBins.ToArray());
                this.Close();
            }
        }
        private void combineBinsButton_Click(object sender, EventArgs e)
        {
            combinedBins.Clear();//empties list
            interfaceChanger.SelectTab("combineBinsPage");
            combineBinsCheckedListBox.Items.Clear();//clears it before adding stuff
            foreach (string name in binNames.Values)
            {//puts names in check box
                combineBinsCheckedListBox.Items.Add(name);
            }
        }
        private void chooseBinsCombineButton_Click(object sender, EventArgs e)
        {
            if (combinedBins.Count <= 1)
            {
                MessageBox.Show("You must choose at least two values");
                return;
            }
            combineBins(combinedBins.ToArray());
            binsCheckedListBox.Items.Clear();
            foreach (string name in binNames.Values)
            {//puts names in check box
                binsCheckedListBox.Items.Add(name);
            }
            checkedBins.Clear();//clears the selected data because the checkbox is cleared
            interfaceChanger.SelectTab("chooseBinsPage");
        }
        private void binsCheckedListBox_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            //need to check if it is already checked and if so then remove it
            if (e.NewValue == CheckState.Checked)
            {
                checkedBins.Add(binsCheckedListBox.SelectedIndex);
            }
            else
            {
                checkedBins.Remove(binsCheckedListBox.SelectedIndex);
            }
        }
        private void combineBinsCheckedListBox_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            //need to check if it is already checked and if so then remove it
            if (e.NewValue == CheckState.Checked)
            {
                combinedBins.Add(combineBinsCheckedListBox.SelectedIndex);
            }
            else
            {
                combinedBins.Remove(combineBinsCheckedListBox.SelectedIndex);
            }
        }
        #endregion
        //buttons associated with deleting bin data
        #region deleteButtons
        private void deleteDataButton_Click(object sender, EventArgs e)
        {
            interfaceChanger.SelectTab("deletePage");
        }
        private void selectBinToDeleteButton_Click(object sender, EventArgs e)
        {
            int temp;
            try
            {
                temp = this.binsToDeleteComboBox.SelectedIndex;
            }
            catch
            {
                MessageBox.Show("Please select a bin");
                return;
            }
            this.selectBinToDeleteButton.Visible = false;
            this.binsToDeleteComboBox.Visible = false;
            this.chooseBinToDeleteFromLabel.Visible = false;
            if (bins[temp].binData.Count > 1)
            {
                this.choosePointToDeleteLabel.Visible = true;
                this.dataPointToDeleteTextBox.Visible = true;
                this.deleteDataPointButton.Visible = true;
            }
            this.deleteEntireBinButton.Visible = true;
        }
        //deletes a specific data point in a bin
        private void deleteDataPointButton_Click(object sender, EventArgs e)
        {
            int pointToDelete = 0;
            int binNumber = 0;
            //get choosen point
            try
            {
                pointToDelete = int.Parse(this.dataPointToDeleteTextBox.Text) - 1;
            }
            catch
            {
                MessageBox.Show("Invalid point");
                this.dataPointToDeleteTextBox.Text = "";
                return;
            }
            //see if it exists in the selected bin
            try
            {
                binNumber = this.binsToDeleteComboBox.SelectedIndex;
                if (pointToDelete > bins[binNumber].binData.Count)
                {
                    throw new Exception();
                }
            }
            catch
            {
                MessageBox.Show(String.Format("There is no point {0} in bin {1}.", pointToDelete, bins[binNumber].BinName));
                this.dataPointToDeleteTextBox.Text = "";
                return;
            }
            //delete the point
            bins[binNumber].binData.RemoveAt(pointToDelete);
            //delete the row of the display
            int rowToDelete = 0;
            for (int i = 0; i < binNumber; i++)
            {
                foreach (MinSearchData data in bins[i].binData)
                {
                    rowToDelete++;//gets the correct row to delete in the display
                }
            }
            rowToDelete += pointToDelete;//then adds on the point to delete in the bin
            this.displayData.Rows.RemoveAt(rowToDelete);
            //updates stats display
            updateBinAveragesAndStdDev();
            //updeate data display with regards to point numbers and bin names
            //puts in the bin name if name was removed and changes the numbers for the data points to reflect removed point
            if (pointToDelete == 0)//if the first row was deleted the name of the bin needs added to the display
            {
                displayData.Rows[rowToDelete].SetField(0, bins[binNumber].BinName);
                rowToDelete++;//stops i from going negative...
                pointToDelete++;//adjusts numbering on display
            }
            //now adjust the point numbers of the display
            int j = pointToDelete;//keeps track of the point number
            for (int i = rowToDelete - 1; i < (rowToDelete + bins[binNumber].binData.Count) - 1; i++)
            {//now updates the item values
                displayData.Rows[i].SetField(1, j);
                j++;
            }

            //goes back to main screen
            if (!exited)
                interfaceChanger.SelectTab("mainProgramPage");
            else
                interfaceChanger.SelectTab("chooseBinsPage");
            //resets screen for next time it is accessed
            this.selectBinToDeleteButton.Visible = true;
            this.binsToDeleteComboBox.Visible = true;
            this.chooseBinToDeleteFromLabel.Visible = true;
            this.choosePointToDeleteLabel.Visible = false;
            this.dataPointToDeleteTextBox.Visible = false;
            this.dataPointToDeleteTextBox.Text = "";
            this.deleteDataPointButton.Visible = false;
            this.deleteEntireBinButton.Visible = false;
            this.deleteDataButton.Visible = true;
            //remakes bin graphs accordingly
            this.createBinGraphs();
        }
        //deletes entire bin of data
        private void deleteEntireBinButton_Click(object sender, EventArgs e)
        {
            int binToDelete = this.binsToDeleteComboBox.SelectedIndex;
            if (MessageBox.Show(String.Format("Are you sure you want to delete all of bin \"{0}\"?", bins[binToDelete].BinName), "Delete Bin?", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                deleteBin(binToDelete);
                //remakes bin graphs
                this.createBinGraphs();
            }
            this.selectBinToDeleteButton.Visible = true;
            this.binsToDeleteComboBox.Visible = true;
            this.chooseBinToDeleteFromLabel.Visible = true;
            this.choosePointToDeleteLabel.Visible = false;
            this.dataPointToDeleteTextBox.Visible = false;
            this.deleteDataPointButton.Visible = false;
            this.deleteEntireBinButton.Visible = false;
            this.deleteDataButton.Visible = true;
            if (bins.Count == 0)
                deleteDataButton.Visible = false;
        }
        #endregion
        //buttons that are all cancel buttons that let the use go back to the main screen
        #region cancelButtons
        private void cancelDeleteButton_Click(object sender, EventArgs e)
        {
            this.selectBinToDeleteButton.Visible = true;
            this.binsToDeleteComboBox.Visible = true;
            this.chooseBinToDeleteFromLabel.Visible = true;
            this.choosePointToDeleteLabel.Visible = false;
            this.dataPointToDeleteTextBox.Visible = false;
            this.deleteDataPointButton.Visible = false;
            this.deleteEntireBinButton.Visible = false;
            this.deleteDataButton.Visible = true;
            if (bins.Count == 0)
                deleteDataButton.Visible = false;
            if (!exited)
                interfaceChanger.SelectTab("mainProgramPage");
            else
                interfaceChanger.SelectTab("chooseBinsPage");
        }
        private void cancelBinCombineButton_Click(object sender, EventArgs e)
        {
            interfaceChanger.SelectTab("chooseBinsPage");
        }
        private void cancelSaveButton_Click(object sender, EventArgs e)
        {
            if (bins.Count != 0)
                this.useExistingBinButton.Visible = true;
            this.createNewBinButton.Visible = true;
            this.chooseBinNameLabel.Visible = false;
            this.newBinNameTextBox.Visible = false;
            this.nameNewBinButton.Visible = false;
            this.binOptionsComboBox.Visible = false;
            this.selectBinButton.Visible = false;
            this.chooseBinLabel.Visible = false;
            interfaceChanger.SelectTab("mainProgramPage");
        }
        //disabled at the moment due to issues with the backgroundworker not cooperating...
        private void cancelChangeGraphButton_Click(object sender, EventArgs e)
        {
            //turn things back on if the cancel was from the change min data button
            this.realLabel.Visible = true;
            this.imaginaryLabel.Visible = true;
            this.initialGuessLabel.Visible = true;
            this.realGuessTextBox.Visible = true;
            this.imaginaryGuessTextBox.Visible = true;
            this.polynomialValueLabel.Visible = true;
            this.polynomialTextBox.Visible = true;
            this.toleranceLabel.Visible = true;
            this.toleranceTextBox.Visible = true;
            this.advanceDataSetButton.Visible = true;
            this.decreaseDataSetButton.Visible = true;
            this.regraphButton.Visible = true;
            this.dataSetLabel.Visible = true;
            interfaceChanger.SelectTab("mainProgramPage");
            if (dataAndGraphChanger.SelectedTab != dataAndGraphChanger.TabPages["dataPage"])
            {
                dataAndGraphChanger.SelectTab("dataPage");
                this.changeGraphOrDataButton.Text = "View Scatter Plot";
            }
            masterDataLocation = oldDataLocation;
        }
        #endregion
        private void viewDataGraphButton_Click(object sender, EventArgs e)
        {
            dataAndGraphChanger.SelectTab("binGraphTabPage");
        }
        private void viewBinDataButton_Click(object sender, EventArgs e)
        {
            dataAndGraphChanger.SelectTab("dataPage");
        }
        #endregion

        //some private classes and structs
        private struct GraphingDataValues
        {
            public List<int> polynomials;//used to make multiple polynomials
            public int Polynomial
            {
                get
                {
                    return _polynomial;
                }
                set
                {
                    if (value > 0)//find limit on polynomial and other values from Dr. Falcetta
                        _polynomial = value;
                }
            }
            private int _polynomial;
            public int NumRootsUsed
            {
                get
                {
                    return _numRootsUsed;
                }
                set
                {
                    if (value > 0 && value <= MainForm.NumRoots)
                        _numRootsUsed = value;
                }
            }
            private int _numRootsUsed;
            public int RootInUse1//may need to change this to an array of size num roots if more than two can be used
            {//check that with Dr. Falcetta
                get
                {
                    return _rootInUse1;
                }
                set
                {
                    if (value > 0 && value <= MainForm.NumRoots)
                        _rootInUse1 = value;
                }
            }
            private int _rootInUse1;
            public int RootInUse2//may need to change this to an array of size num roots if more than two can be used
            {//check that with Dr. Falcetta
                get
                {
                    return _rootInUse2;
                }
                set
                {
                    if (value > 0 && value <= MainForm.NumRoots && value >= RootInUse1)
                        _rootInUse2 = value;
                }
            }
            private int _rootInUse2;
            public string Tolerance { get; set; }//currently a string because it needs to have scientific input
            public double RealGuess { get; set; }
            public double ImaginaryGuess { get; set; }
            public double RealRange
            {
                get
                {
                    return _realRange;
                }
                set
                {
                    if (value > 0)
                        _realRange = value;
                }
            }
            private double _realRange;
            public double ImaginaryRange
            {
                get
                {
                    return _imaginaryRange;
                }
                set
                {
                    if (value > 0)
                        _imaginaryRange = value;
                }
            }
            private double _imaginaryRange;
            public string ProgramType
            {
                get
                {
                    return _programType;
                }
                set//this value can only be grid or min
                {
                    if (value == "grid" || value == "minm")
                        _programType = value;
                }
            }
            private string _programType;
            public string Title { get; set; }
            public int GridSize
            {
                get
                {
                    return _gridSize;
                }
                set
                {
                    if (value > 0)
                        _gridSize = value;
                }
            }
            private int _gridSize;
            public float StartIRange;
            public float EndIRange;
            public float StartRRange;
            public float EndRRange;
        }
        private class GraphPoint
        {
            public GraphPoint(double x, double y)
            {
                X = x;
                Y = y;
            }
            public double X { get; set; }
            public double Y { get; set; }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (exited)
            {//quits without doing anything else
                try
                {
                    StreamWriter temp = new StreamWriter(String.Format("{0}\\mouseData.txt", mouseDataLocation));
                    temp.WriteLine();
                    temp.Close();
                }
                catch
                {
                }
                //asks user if they want to save bin graphs
                if (bins.Count > 0)
                {
                    if (MessageBox.Show("Do you want to save your bin graphs?", "save graphs?", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        this.saveBinGraphs();
                    }
                }
                MessageBox.Show("GoodBye!");
                return;
            }
            //get rid of threads just in case
            killThreads();
            //delete the files created by the program
            deleteProgramFiles();
            if (bins.Count > 0)//asks if the user wants to use their data to generate output file
            {
                if (MessageBox.Show("Do you want to generate an output file?", "End Program", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    exited = true;
                    e.Cancel = true;
                    foreach (string name in binNames.Values)
                    {
                        binsCheckedListBox.Items.Add(name);
                    }
                    if (bins.Count < 2)
                    {
                        this.combineBinsButton.Visible = false;
                    }
                    //run stats tests on all the bins
                    List<string> statsInfo = new List<string>();
                    for (int i = 0; i < bins.Count; i++)
                    {
                        for (int j = bins.Count-1; j >= 0; j--)
                        {
                            if (i == j)
                                break;
                            if (i != j)
                            {
                                if (compareAverages(bins[i].AverageRealE, bins[j].AverageRealE, bins[i].StdDevRealE, bins[j].StdDevRealE, bins[i].binData.Count, bins[j].binData.Count) ||
                                compareAverages(bins[i].AverageImagE, bins[j].AverageImagE, bins[i].StdDevImagE, bins[j].StdDevImagE, bins[i].binData.Count, bins[j].binData.Count))
                                {
                                    //then they are considered different and nothing needs to be done
                                }
                                else
                                {
                                    this.statsTestsTextBox.Visible = true;
                                    statsInfo.Add(String.Format("Bin \"{0}\" and Bin \"{1}\" are not statistically different\n", bins[i].BinName, bins[j].BinName));
                                }
                                
                            }
                        }
                    }
                    this.statsTestsTextBox.Lines = statsInfo.ToArray();

                    interfaceChanger.SelectTab("chooseBinsPage");
                    dataAndGraphChanger.SelectTab("dataPage");
                    checkedBins.Clear();//clears list of checked bins
                    return;
                }
                if (MessageBox.Show("Do you want to save bin data?", "End Program", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    this.saveFileDialog1.ShowDialog();
                    try
                    {
                        saveBinData(saveFileDialog1.FileName);
                    }
                    catch
                    {
                    }
                }
            }

            //changes the mouse data file so that the background worker will be able to end
            try
            {
                StreamWriter temp = new StreamWriter(String.Format("{0}\\mouseData.txt",mouseDataLocation));
                temp.WriteLine();
                temp.Close();
            }
            catch
            {
            }
            if (bins.Count > 0)
            {
                if (MessageBox.Show("Do you want to save your bin graphs?", "save graphs?", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    this.saveBinGraphs();
                }
            }
            MessageBox.Show("GoodBye!");
        }

        //temp stuff for console debug
        [DllImport("Kernel32.dll")]
        static extern Boolean AllocConsole();
      
        //test functions here
        private void MainForm_Load(object sender, EventArgs e)
        {   
            //uncomment to enable console based debuggin
            AllocConsole();
        }

        
        //end temp stuffs
    }

    public class IndividualDataPoint//has just one x and y value, will be used in a list for one x value that has multiple y values
    {
        public IndividualDataPoint(double x, double y)
        {
            XValue = x;
            YValue = y;
        }
        public double XValue { get; set; }
        public double YValue { get; set; }
    }
    public class MultipleDataPoints//has NumRoots number of data values
    {
        public List<IndividualDataPoint> data = new List<IndividualDataPoint>();
    }
    //this system is useful because it makes it so that for each data point the value of y for a particular root can be accessed
    //with an index which makes generating the master data set much less complicated

    public struct MinSearchData//this will contain the data stored for one search for a minimum
    {
        public int PolynomialOrder
        {
            get
            {
                return _polynomialOrder;
            }
            set
            {
                if (value > 0)
                    _polynomialOrder = value;
            }
        }
        private int _polynomialOrder;
        public int RootUsed1
        {
            get
            {
                return _rootUsed1;
            }
            set
            {
                if (value > 0)
                    _rootUsed1 = value;
            }
        }
        private int _rootUsed1;
        public int RootUsed2
        {
            get
            {
                return _rootUsed2;
            }
            set
            {
                if (value > 0 && value > RootUsed1)
                    _rootUsed2 = value;
            }
        }
        private int _rootUsed2;
        public double DerivativeValue { get; set; }
        public double RealAlpha { get; set; }
        public double ImagAlpha { get; set; }
        public double RealE { get; set; }
        public double ImagE { get; set; }
        public int SetUsed { get; set; }//referst to set of data, just the value of the starting point will do
    }
    public class Bin//this class is a bin of data that the user can put in similar results
    {
        public double AverageRealE { get; set; }
        public double AverageImagE { get; set; }
        public double StdDevRealE { get; set; }
        public double StdDevImagE { get; set; }
        public double AverageRealAlpha { get; set; }
        public double AverageImagAlpha { get; set; }
        public double StdDevRealAlpha { get; set; }
        public double StdDevImagAlpha { get; set; }
        public string BinName { get; set; }
        public List<MinSearchData> binData = new List<MinSearchData>();
    }

    //created based off of class from msdn http://code.msdn.microsoft.com/Excel-2010-Generating-c79f6e72
    public class ExcelCreator
    {
        private string path = @"C:\Users\gearhartjj1\documents\college\summer 2013 job\prototypes\prototype 6 New Design\";
        private string templateName = "outputTemplate.xlsx";
        private WorkbookPart workbookPart = null;
        private SpreadsheetDocument document = null;

        public ExcelCreator(string outputName)
        {
            openFile(outputName);
        }
        public ExcelCreator(string outputName, string templatePath, string name)
        {
            path = templatePath;
            templateName = name;
            openFile(outputName);
        }
        private void openFile(string outputName)
        {
            string endOfName = outputName.Substring(outputName.Length - 5);//gets last four characters to see if .xlsx is there
            string newFileName = String.Format(endOfName != ".xlsx" ? "{0}.xlsx" : "{0}", outputName);
            if (!CopyFile(path + templateName, newFileName))
            {
                MessageBox.Show("Output file failed");
                return;
            }
            else
            {
                document = SpreadsheetDocument.Open(newFileName, true);
                workbookPart = document.WorkbookPart;
            }
        }
        private bool CopyFile(string source, string destination)
        {
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }
            bool success = true;
            try
            {
                File.Copy(source, destination);
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
                success = false;
            }
            return success;
        }
        public bool UpdateValue(string sheetName, string addressName, string value, UInt32Value styleIndex, bool isString)
        {
            // Assume failure.
            bool updated = false;

            Sheet sheet = workbookPart.Workbook.Descendants<Sheet>().Where((s) => s.Name == sheetName).FirstOrDefault();

            if (sheet != null)
            {
                Worksheet ws = ((WorksheetPart)(workbookPart.GetPartById(sheet.Id))).Worksheet;
                Cell cell = InsertCellInWorksheet(ws, addressName);

                if (isString)
                {
                    // Either retrieve the index of an existing string,
                    // or insert the string into the shared string table
                    // and get the index of the new item.
                    int stringIndex = InsertSharedStringItem(workbookPart, value);

                    cell.CellValue = new CellValue(stringIndex.ToString());
                    cell.DataType = new EnumValue<CellValues>(CellValues.SharedString);
                }
                else
                {
                    cell.CellValue = new CellValue(value);
                    cell.DataType = new EnumValue<CellValues>(CellValues.Number);
                }

                if (styleIndex > 0)
                    cell.StyleIndex = styleIndex;
                
                // Save the worksheet.
                ws.Save();
                updated = true;
            }
            return updated;
        }
        private uint GetRowIndex(string address)
        {
            string rowPart;
            UInt32 l;
            UInt32 result = 0;

            //try every part of the address until the row is gotten...
            for (int i = 0; i < address.Length; i++)
            {
                if (UInt32.TryParse(address.Substring(i, 1), out l))
                {
                    rowPart = address.Substring(i, address.Length - i);
                    if (UInt32.TryParse(rowPart, out l))
                    {
                        result = l;
                        break;
                    }
                }
            }
            return result;
        }
        private Row GetRow(SheetData wsData, uint rowIndex)
        {
            var row = wsData.Elements<Row>().
            Where(r => r.RowIndex.Value == rowIndex).FirstOrDefault();
            if (row == null)
            {
                row = new Row();
                row.RowIndex = rowIndex;
                wsData.Append(row);
            }
            return row;
        }
        // Given a Worksheet and an address (like "AZ254"), either return a cell reference, or 
        // create the cell reference and return it.
        private Cell InsertCellInWorksheet(Worksheet ws, string addressName)
        {
            SheetData sheetData = ws.GetFirstChild<SheetData>();
            Cell cell = null;

            UInt32 rowNumber = GetRowIndex(addressName);
            Row row = GetRow(sheetData, rowNumber);

            // If the cell you need already exists, return it.
            // If there is not a cell with the specified column name, insert one.  
            Cell refCell = row.Elements<Cell>().
                Where(c => c.CellReference.Value == addressName).FirstOrDefault();
            if (refCell != null)
            {
                cell = refCell;
            }
            else
            {
                cell = CreateCell(row, addressName);
            }
            return cell;
        }
        private Cell CreateCell(Row row, string address)
        {
            Cell cellResult;
            Cell refCell = null;

            // Cells must be in sequential order according to CellReference. Determine where to insert the new cell.
            foreach (Cell cell in row.Elements<Cell>())
            {
                if (string.Compare(cell.CellReference.Value, address, true) > 0)//checks to see if it found the desired cell
                {
                    refCell = cell;
                    break;
                }
            }

            cellResult = new Cell();
            cellResult.CellReference = address;

            row.InsertBefore(cellResult, refCell);
            return cellResult;
        }
        // Given the main workbook part, and a text value, insert the text into the shared
        // string table. Create the table if necessary. If the value already exists, return
        // its index. If it doesn't exist, insert it and return its new index.
        private int InsertSharedStringItem(WorkbookPart wbPart, string value)
        {
            int index = 0;
            bool found = false;
            var stringTablePart = wbPart.GetPartsOfType<SharedStringTablePart>().FirstOrDefault();

            // If the shared string table is missing, something's wrong.
            // Just return the index that you found in the cell.
            // Otherwise, look up the correct text in the table.
            if (stringTablePart == null)
            {
                // Create it.
                stringTablePart = wbPart.AddNewPart<SharedStringTablePart>();
            }

            var stringTable = stringTablePart.SharedStringTable;
            if (stringTable == null)
            {
                stringTable = new SharedStringTable();
            }

            // Iterate through all the items in the SharedStringTable. If the text already exists, return its index.
            foreach (SharedStringItem item in stringTable.Elements<SharedStringItem>())
            {
                if (item.InnerText == value)
                {
                    found = true;
                    break;
                }
                index += 1;
            }

            if (!found)
            {
                stringTable.AppendChild(new SharedStringItem(new Text(value)));
                stringTable.Save();
            }

            return index;
        }
        // Used to force a recalc of cells containing formulas. The
        // CellValue has a cached value of the evaluated formula. This
        // will prevent Excel from recalculating the cell even if 
        // calculation is set to automatic.
        private bool RemoveCellValue(string sheetName, string addressName)
        {
            bool returnValue = false;

            Sheet sheet = workbookPart.Workbook.Descendants<Sheet>().
                Where(s => s.Name == sheetName).FirstOrDefault();
            if (sheet != null)
            {
                Worksheet ws = ((WorksheetPart)(workbookPart.GetPartById(sheet.Id))).Worksheet;
                Cell cell = InsertCellInWorksheet(ws, addressName);

                // If there is a cell value, remove it to force a recalc
                // on this cell.
                if (cell.CellValue != null)
                {
                    cell.CellValue.Remove();
                }

                // Save the worksheet.
                ws.Save();
                returnValue = true;
            }

            return returnValue;
        }
        public void closeFile()
        {
            document.Close();
        }
    }

    //from stack overflow http://stackoverflow.com/questions/13602824/c-sharp-multiple-screen-view-single-form
    class TablessTabControl : TabControl
    {
        protected override void WndProc(ref Message m)
        {
            // Hide tabs by trapping the TCM_ADJUSTRECT message
            if (m.Msg == 0x1328 && !DesignMode) m.Result = (IntPtr)1;
            else base.WndProc(ref m);
        }
    }
}