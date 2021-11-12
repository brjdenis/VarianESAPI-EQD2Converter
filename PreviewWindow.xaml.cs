using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Wpf;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using Image = VMS.TPS.Common.Model.API.Image;

namespace EQD2Converter
{
    /// <summary>
    /// Interaction logic for PreviewWindow.xaml
    /// </summary>
    public partial class PreviewWindow : Window
    {
        public PlotModel PlotModelOriginal { get; set; }
        public PlotModel PlotModelConverted { get; set; }

        public LinearColorAxis arrayLinearColorAxisImage = new LinearColorAxis { Palette = OxyPalettes.Gray(1024) };
        public LinearColorAxis arrayLinearColorAxisDoseOriginal = new LinearColorAxis { Palette = OxyPalettes.Jet(1024), LowColor = OxyColors.Black, Position = AxisPosition.None };
        public LinearColorAxis arrayLinearColorAxisDoseConverted = new LinearColorAxis { Palette = OxyPalettes.Jet(1024), LowColor = OxyColors.Black, Position = AxisPosition.None };


        public HeatMapSeries imageHeatMapImage = new HeatMapSeries { };
        public HeatMapSeries imageHeatMapDoseOriginal = new HeatMapSeries { };
        public HeatMapSeries imageHeatMapDoseConverted = new HeatMapSeries { };

        public ScriptContext scriptcontext;

        public List<double[,]> originalArray;
        public List<double[,]> convertedArray;
        public List<double[,]> image;

        public double scaling;

        public int CurrentSlice = 0;

        public double doseMax;
        public double doseMin;

        public double doseMaxConverted;
        public double doseMinConverted;

        public int XsizeDose;
        public int YsizeDose;
        public int ZsizeDose;
        public double XresDose;
        public double YresDose;
        public double ZresDose;
        public VVector OriginDose;

        public int XsizeImage;
        public int YsizeImage;
        public int ZsizeImage;
        public double XresImage;
        public double YresImage;
        public double ZresImage;
        public VVector OriginImage;

        public VVector Xdir;
        public VVector Ydir;
        public VVector Zdir;

        public PreviewWindow(ScriptContext scriptcontext, int[,,] convertedArray, int[,,] originalArray, double scaling,
            double doseMin, double doseMax, double doseMinConv, double doseMaxConv)
        {
            this.scriptcontext = scriptcontext;
            this.scaling = scaling;

            Dose dose = this.scriptcontext.ExternalPlanSetup.Dose;
            Image image = this.scriptcontext.Image;

            this.XsizeDose = dose.XSize;
            this.YsizeDose = dose.YSize;
            this.ZsizeDose = dose.ZSize;
            this.XresDose = dose.XRes;
            this.YresDose = dose.YRes;
            this.ZresDose = dose.ZRes;
            this.OriginDose = dose.Origin;

            this.XsizeImage = image.XSize;
            this.YsizeImage = image.YSize;
            this.ZsizeImage = image.ZSize;
            this.XresImage = image.XRes;
            this.YresImage = image.YRes;
            this.ZresImage = image.ZRes;
            this.OriginImage = image.Origin;

            this.Xdir = dose.XDirection;
            this.Ydir = dose.YDirection;
            this.Zdir = dose.ZDirection;

            this.doseMax = doseMax;
            this.doseMin = doseMin;
            this.doseMaxConverted = doseMaxConv * this.scaling;
            this.doseMinConverted = doseMinConv * this.scaling;

            this.convertedArray = ConvertArrayToDouble(convertedArray);
            this.originalArray = ConvertArrayToDouble(originalArray);
            this.image = GetImage();

            InitializeComponent();

            this.PlotModelOriginal = CreatePlotModelOriginal();
            this.PlotOriginal.Model = PlotModelOriginal;
            this.PlotModelConverted = CreatePlotModelConverted();
            this.PlotConverted.Model = PlotModelConverted;

            this.TextBoxHighDose.Text = this.doseMax.ToString("F2");
            this.TextBoxLowDose.Text = this.doseMin.ToString("F2");

            DefineSliderLevelStartingValue();

            PopulateStructureList();

            this.PlotModelConverted.TrackerChanged += SynchroniseTrackerLineConverted;
            this.PlotModelOriginal.TrackerChanged += SynchroniseTrackerLineOriginal;

            this.imageHeatMapDoseConverted.XAxis.AxisChanged += AxisChanged;
            this.imageHeatMapDoseConverted.YAxis.AxisChanged += AxisChanged;

            this.SizeChanged += LayoutUpdate;
        }


        private void SynchroniseTrackerLineConverted(Object sender, TrackerEventArgs args)
        {
            if (args.HitResult != null)
            {
                var currentResult = args.HitResult;
                var currentPosition = currentResult.Position;

                DataPoint dataPosition = this.imageHeatMapDoseConverted.InverseTransform(currentPosition);
                ScreenPoint screenPositionOriginal = this.imageHeatMapDoseOriginal.Transform(dataPosition);
                TrackerHitResult result = this.imageHeatMapDoseOriginal.GetNearestPoint(screenPositionOriginal, false);
         
                this.PlotOriginal.ShowTracker(result);
            }
            else
            {
                this.PlotOriginal.HideTracker();
            }
        }

        private void SynchroniseTrackerLineOriginal(Object sender, TrackerEventArgs args)
        {
            if (args.HitResult != null)
            {
                var currentResult = args.HitResult;
                var currentPosition = currentResult.Position;

                DataPoint dataPosition = this.imageHeatMapDoseOriginal.InverseTransform(currentPosition);
                ScreenPoint screenPositionConverted = this.imageHeatMapDoseConverted.Transform(dataPosition);
                TrackerHitResult result = this.imageHeatMapDoseConverted.GetNearestPoint(screenPositionConverted, false);

                this.PlotConverted.ShowTracker(result);
            }
            else
            {
                this.PlotConverted.HideTracker();
            }
        }


        private void AxisChanged(object sender, AxisChangedEventArgs e)
        {
            var axisXConverted = this.imageHeatMapDoseConverted.XAxis;
            var axisYConverted = this.imageHeatMapDoseConverted.YAxis;
            var axisXOriginal = this.imageHeatMapDoseOriginal.XAxis;
            var axisYOriginal = this.imageHeatMapDoseOriginal.YAxis;

            switch (e.ChangeType)
            {
                case AxisChangeTypes.Reset:
                    axisXOriginal.Reset();
                    axisYOriginal.Reset();
                    break;
                case AxisChangeTypes.Zoom:
                case AxisChangeTypes.Pan:
                    axisXOriginal.AbsoluteMinimum = axisXConverted.ActualMinimum;
                    axisXOriginal.AbsoluteMaximum = axisXConverted.ActualMaximum;
                    axisXOriginal.Minimum = axisXConverted.ActualMinimum;
                    axisXOriginal.Maximum = axisXConverted.ActualMaximum;

                    axisYOriginal.AbsoluteMinimum = axisYConverted.ActualMinimum;
                    axisYOriginal.AbsoluteMaximum = axisYConverted.ActualMaximum;
                    axisYOriginal.Minimum = axisYConverted.ActualMinimum;
                    axisYOriginal.Maximum = axisYConverted.ActualMaximum;
                    break;
            }
            this.PlotModelOriginal.InvalidatePlot(true);
        }

        private void LayoutUpdate(object sender, EventArgs e)
        {
            this.PlotOriginal.Width = this.PlotOriginalColumn.ActualWidth;
            this.PlotOriginal.Height = this.PlotOriginalRow.ActualHeight;
            this.PlotConverted.Width = this.PlotConvertedColumn.ActualWidth;
            this.PlotConverted.Height = this.PlotOriginalRow.ActualHeight;
            //this.PlotModelOriginal.ResetAllAxes();
            //this.PlotModelOriginal.InvalidatePlot(true);
        }   


        public List<double[,]> ConvertArrayToDouble(int[,,] array)
        {
            // also inverts Y axis!
            List<double[,]> temp = new List<double[,]>() { };
            int Ysize = array.GetLength(2);

            for (int i = 0; i < array.GetLength(0); i++)
            {
                double[,] image = new double[array.GetLength(1), array.GetLength(2)];

                for (int j = 0; j < array.GetLength(1); j++)
                {
                    for (int k = 0; k < array.GetLength(2); k++)
                    {
                        image[j, Ysize - k - 1] = (double)array[i, j, k] * this.scaling;
                    }
                }
                temp.Add(image);
            }
            return temp;
        }

        public List<double[,]> GetImage()
        {
            Image image = this.scriptcontext.Image;
            int Xsize = image.XSize;
            int Ysize = image.YSize;
            int Zsize = image.ZSize;

            List<double[,]> temp = new List<double[,]>() { };

            for (int k = 0; k < Zsize; k++)
            {
                int[,] temp2 = new int[Xsize, Ysize];
                image.GetVoxels(k, temp2);
                double[,] temp22 = new double[Xsize, Ysize];

                for (int i = 0; i < Xsize; i++)
                {
                    for (int j = 0; j < Ysize; j++)
                    {
                        temp22[i, Ysize - j - 1] = (double)temp2[i, j] * this.scaling;
                    }
                }
                temp.Add(temp22);
            }
            return temp;
        }

        public void PopulateStructureList()
        {
            List<string> structureList = new List<string>() { };

            foreach (var structure in this.scriptcontext.StructureSet.Structures.ToList())
            {
                if (!structure.IsEmpty)
                {
                    structureList.Add(structure.Id);
                }
            }
            this.StructureListView.ItemsSource = structureList;
            this.StructureListView.SelectAll();
        }

        public PlotModel CreatePlotModelOriginal()
        {
            var plotModel = new PlotModel { PlotType = PlotType.Cartesian };

            CreateSeriesDoseOriginal(plotModel);
            AddAxes(plotModel, this.arrayLinearColorAxisDoseOriginal);

            return plotModel;
        }

        public PlotModel CreatePlotModelConverted()
        {
            var plotModel = new PlotModel { PlotType = PlotType.Cartesian };

            CreateSeriesDoseConverted(plotModel);
            AddAxes(plotModel, this.arrayLinearColorAxisDoseConverted);

            return plotModel;
        }

        public void AddAxes(PlotModel plotModel, Axis axis)
        {
            axis.Maximum = this.doseMax;
            axis.Minimum = this.doseMin;
            plotModel.Axes.Add(axis);
        }

        public void CreateSeriesDoseOriginal(PlotModel plotModel)
        {
            double[,] img = this.originalArray[0];
            var series = new HeatMapSeries
            {
                X0 = 0,
                X1 = img.GetLength(0) - 1,
                Y0 = 0,
                Y1 = img.GetLength(1) - 1,
                Interpolate = false,
                RenderMethod = HeatMapRenderMethod.Bitmap,
                LabelFormatString = "0.0",
                TrackerFormatString = "{0}\n{1}: {2}\n{3}: {4}\n{5}: {6:0.###}",
                Data = img
            };

            var myController = new PlotController();
            this.PlotOriginal.Controller = myController;
            
            myController.UnbindMouseWheel();

            myController.UnbindMouseDown(OxyMouseButton.Middle, OxyModifierKeys.Control);
            myController.UnbindMouseDown(OxyMouseButton.Right, OxyModifierKeys.None);
            myController.UnbindMouseDown(OxyMouseButton.Right, OxyModifierKeys.None, 2);
            myController.UnbindMouseDown(OxyMouseButton.Middle, OxyModifierKeys.None);
            myController.UnbindMouseDown(OxyMouseButton.Middle, OxyModifierKeys.None, 2);

            myController.BindMouseWheel(OxyModifierKeys.Control, OxyPlot.PlotCommands.ZoomWheelFine);
            myController.BindMouseDown(OxyMouseButton.Right, OxyModifierKeys.None, OxyPlot.PlotCommands.ZoomRectangle);
            myController.BindMouseDown(OxyMouseButton.Right, OxyModifierKeys.None, 2, OxyPlot.PlotCommands.ResetAt);
            myController.BindMouseDown(OxyMouseButton.Middle, OxyModifierKeys.None, OxyPlot.PlotCommands.PanAt);

            this.imageHeatMapDoseOriginal = series;
            plotModel.Series.Add(series);
        }

        public void CreateSeriesDoseConverted(PlotModel plotModel)
        {
            double[,] img = this.convertedArray[0];
            var series = new HeatMapSeries
            {
                X0 = 0,
                X1 = img.GetLength(0) - 1,
                Y0 = 0,
                Y1 = img.GetLength(1) - 1,
                Interpolate = false,
                RenderMethod = HeatMapRenderMethod.Bitmap,
                LabelFormatString = "0.0",
                TrackerFormatString = "{0}\n{1}: {2}\n{3}: {4}\n{5}: {6:0.###}",
                Data = img
            };

            var myController = new PlotController();
            this.PlotConverted.Controller = myController;
            myController.UnbindMouseWheel();

            myController.UnbindMouseDown(OxyMouseButton.Middle, OxyModifierKeys.Control);
            myController.UnbindMouseDown(OxyMouseButton.Right, OxyModifierKeys.None);
            myController.UnbindMouseDown(OxyMouseButton.Right, OxyModifierKeys.None, 2);
            myController.UnbindMouseDown(OxyMouseButton.Middle, OxyModifierKeys.None);
            myController.UnbindMouseDown(OxyMouseButton.Middle, OxyModifierKeys.None, 2);

            myController.BindMouseWheel(OxyModifierKeys.Control, OxyPlot.PlotCommands.ZoomWheelFine);
            myController.BindMouseDown(OxyMouseButton.Right, OxyModifierKeys.None, OxyPlot.PlotCommands.ZoomRectangle);
            myController.BindMouseDown(OxyMouseButton.Right, OxyModifierKeys.None, 2, OxyPlot.PlotCommands.ResetAt);
            myController.BindMouseDown(OxyMouseButton.Middle, OxyModifierKeys.None, OxyPlot.PlotCommands.PanAt);


            this.imageHeatMapDoseConverted = series;
            plotModel.Series.Add(series);
        }


        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int slice = (int)this.SliceSlider.Value;
            this.imageHeatMapDoseOriginal.Data = this.originalArray[slice];
            this.imageHeatMapDoseConverted.Data = this.convertedArray[slice];
            this.CurrentSlice = slice;

            AddStructureContour();

            this.PlotModelOriginal.InvalidatePlot(true);
            this.PlotModelConverted.InvalidatePlot(true);
        }

        public void DefineSliderLevelStartingValue()
        {
            this.SliceSlider.Minimum = 0;
            this.SliceSlider.Maximum = this.originalArray.Count - 1;
            this.SliceSlider.Value = 0;

            if (this.doseMaxConverted > this.doseMax)
            {
                this.DoseMaxSlider.Maximum = this.doseMaxConverted;
                this.DoseMinSlider.Maximum = this.doseMaxConverted;
            }
            else
            {
                this.DoseMaxSlider.Maximum = this.doseMax;
                this.DoseMinSlider.Maximum = this.doseMax;
            }

            this.DoseMaxSlider.Minimum = 0;
            this.DoseMinSlider.Minimum = 0;

            if (this.doseMinConverted < this.doseMin)
            {
                this.DoseMinSlider.Value = this.doseMinConverted;
            }
            else
            {
                this.DoseMinSlider.Value = this.doseMin;
            }
            this.DoseMaxSlider.Value = this.DoseMaxSlider.Maximum;

        }

        private void PlotOriginal_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control)
            {
                if (e.Delta > 0)
                {
                    if (this.SliceSlider.Value < this.SliceSlider.Maximum)
                    {
                        this.SliceSlider.Value += 1;
                    }
                }
                else if (e.Delta < 0)
                {
                    if (this.SliceSlider.Value > this.SliceSlider.Minimum)
                    {
                        this.SliceSlider.Value -= 1;
                    }
                }
            }
        }

        private void TextBoxHighLowDose_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox txt = sender as TextBox;
            int ind = txt.CaretIndex;
            txt.Text = txt.Text.Replace(",", ".");
            txt.CaretIndex = ind;

        }

        private double ConvertTextToDouble(string text)
        {
            if (Double.TryParse(text, out double result))
            {
                return result;
            }
            else
            {
                return Double.NaN;
            }
        }

        private void OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                ChangeColormap();
            }
        }

        private void ChangeColormap()
        {
            double highdose = ConvertTextToDouble(this.TextBoxHighDose.Text);
            double lowdose = ConvertTextToDouble(this.TextBoxLowDose.Text);

            if (!Double.IsNaN(highdose) & !Double.IsNaN(lowdose))
            {
                this.arrayLinearColorAxisDoseOriginal.Maximum = highdose;
                this.arrayLinearColorAxisDoseOriginal.Minimum = lowdose;
                this.arrayLinearColorAxisDoseConverted.Maximum = highdose;
                this.arrayLinearColorAxisDoseConverted.Minimum = lowdose;
                this.PlotModelOriginal.InvalidatePlot(true);
                this.PlotModelConverted.InvalidatePlot(true);
            }
        }

        private void DoseMaxSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.DoseMaxSlider != null & this.DoseMinSlider != null)
            {
                if (this.DoseMaxSlider.Value > this.DoseMinSlider.Value)
                {
                    this.TextBoxHighDose.Text = this.DoseMaxSlider.Value.ToString("F2");
                    this.TextBoxLowDose.Text = this.DoseMinSlider.Value.ToString("F2");
                    ChangeColormap();
                }
            }

        }

        private void AddStructureContour()
        {
            List<Structure> structureList = GetStructureList();
            
            RemoveAnnotations(this.PlotModelOriginal);
            RemoveAnnotations(this.PlotModelConverted);
            
            foreach (Structure structure in structureList)
            {
                VVector[][] contours = structure.GetContoursOnImagePlane(GetImagePlane(this.CurrentSlice));

                int i = 0;
                foreach (var c in contours)
                {
                    var color = structure.Color.ToOxyColor();
                    PolygonAnnotation polygonOriginal = new PolygonAnnotation { Fill = OxyColors.Transparent, Stroke = color, StrokeThickness = 1 };
                    PolygonAnnotation polygonConverted = new PolygonAnnotation { Fill = OxyColors.Transparent, Stroke = color, StrokeThickness = 1 };

                    foreach (var cc in c)
                    {
                        Tuple<double, double> indices = GetDosePlaneIndices(cc.x, cc.y);
                        polygonOriginal.Points.Add(new DataPoint(indices.Item1, indices.Item2));
                        polygonConverted.Points.Add(new DataPoint(indices.Item1, indices.Item2));
                    }
                    this.PlotModelOriginal.Annotations.Add(polygonOriginal);
                    this.PlotModelConverted.Annotations.Add(polygonConverted);

                    i++;
                }
            }
        }

        public void RemoveAnnotations(PlotModel plotmodel)
        {
            if (plotmodel.Annotations.Count > 0)
            {
                foreach (var annot in plotmodel.Annotations.ToList()) // must use ToList() otherwise you will get errros
                {
                    plotmodel.Annotations.Remove(annot);
                }
            }
        }

        public int GetImagePlane(int dosePlane)
        {
            return Convert.ToInt32((this.OriginDose.z - this.OriginImage.z + dosePlane * this.ZresDose * this.Zdir.z) / this.ZresImage);
        }

        public Tuple<double, double> GetDosePlaneIndices(double x, double y)
        {
            // x and y are in the image coordinate system. Convert them to dose coordinate system (not mm, but fractions of array size)
            // The following is valid only for HFS, HFP, FFS, FFP orientations that do not mix x,y,z
            double sx = this.Xdir.x + this.Ydir.x + this.Zdir.x;
            double sy = this.Xdir.y + this.Ydir.y + this.Zdir.y;
            double sz = this.Xdir.z + this.Ydir.z + this.Zdir.z;

            double xDose = (x - this.OriginDose.x) / (this.XresDose * sx);
            double yDose = (y - this.OriginDose.y) / (this.YresDose * sy);
            return Tuple.Create(xDose, this.YsizeDose - yDose - 1);
        }

        private List<Structure> GetStructureList()
        {
            List<Structure> structureList = new List<Structure>() { };

            foreach(var selection in this.StructureListView.SelectedItems)
            {
                string item = selection.ToString();
                Structure structure = this.scriptcontext.StructureSet.Structures.First(u => u.Id == item);
                structureList.Add(structure);
            }
            return structureList.OrderBy(u => u.Id).ToList();
        }

        private void StructureListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AddStructureContour();
            this.PlotModelOriginal.InvalidatePlot(true);
            this.PlotModelConverted.InvalidatePlot(true);
        }
    }
}
