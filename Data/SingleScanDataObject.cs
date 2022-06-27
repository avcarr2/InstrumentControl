﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Thermo.Interfaces.InstrumentAccess_V1.MsScanContainer;
using MassSpectrometry;
using IMSScanClassExtensions; 

namespace Data
{
    public class SingleScanDataObject
    {
        public double[] XArray { get; private set; }
        public double[] YArray { get; private set; }
        public double TotalIonCurrent { get; private set; }
        public double MinX { get; private set; }
        public double MaxX { get; private set; }

        public SingleScanDataObject(IMsScan scan)
        {
            XArray = scan.Centroids.Select(c => c.Mz).ToArray();
            YArray = scan.Centroids.Select(c => c.Intensity).ToArray();
            TotalIonCurrent = double.Parse(scan.Header["Total Ion Current"]);
            MinX = scan.GetValueFromHeaderDict<double>("FirstMass");
            MaxX = scan.GetValueFromHeaderDict<double>("LastMass"); 
        }

        public SingleScanDataObject(MsDataScan scan)
        {
            XArray = scan.MassSpectrum.XArray;
            YArray = scan.MassSpectrum.YArray;
            TotalIonCurrent = scan.TotalIonCurrent;
        }
        public void UpdateYarray(double[] newYarray)
        {
            YArray = newYarray; 
        }
    }
}