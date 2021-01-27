﻿using System;
using System.Timers;

namespace RateController
{
    public class CRateCals
    {
        public readonly FormStart mf;

        public PGN32761 Switches32761;                  //to Rate Controller from arduino, to AOG from Rate Controller
        private PGN32613 ArdRec32613;
        private PGN32614 ArdSend32614;                  // to Arduino from Rate Controller
        private PGN32615 ValveCal32615;                  // to Arduino from Rate Controller

        public bool ArduinoConnected = false;
        public byte CoverageUnits = 0;

        public bool EraseApplied = false;
        public double FlowCal = 0;
        public clsArduino Nano;

        public byte QuantityUnits = 0;
        public double RateSet = 0;
        public double TankSize = 0;

        public byte ValveType = 0;        // 0 standard, 1 fast close
        private DateTime ArduinoReceiveTime;

        private double Coverage = 0;
        private SimType cSimulationType = 0;
        private double CurrentMinutes;

        private double CurrentQuantity = 0;
        private double CurrentWidth;
        private double CurrentWorkedArea = 0;

        private double HectaresPerMinute = 0;
        private double LastAccQuantity = 0;
        private double LastQuantityDifference = 0;

        private DateTime LastTime;
        private double LastWorkedArea = 0;
        private bool PauseArea = false;

        private DateTime QcheckLast;
        private double QuantityApplied = 0;
        private double Ratio;

        private DateTime StartTime;
        private double TankRemaining = 0;
        private double TotalWorkedArea = 0;

        public byte MinPWM { get { return ValveCal32615.MinPWM; } set { ValveCal32615.MinPWM = value; } }
        public int SendTime { get { return ValveCal32615.SendTime; } set { ValveCal32615.SendTime = value; } }

        public int VCN { get { return ValveCal32615.VCN; } set { ValveCal32615.VCN = value; } }
        public int WaitTime { get { return ValveCal32615.WaitTime; } set { ValveCal32615.WaitTime = value; } }

        private int cControllerID = 0;
        private int cProductID = 0;
        private string cProductName = "";

        public CRateCals(FormStart CallingForm, int ProdID)
        {
            mf = CallingForm;
            cProductID = ProdID;

            ArdSend32614 = new PGN32614(this, (byte)cProductID);
            ValveCal32615 = new PGN32615(this, (byte)cProductID);

            Switches32761 = new PGN32761(this);
            ArdRec32613 = new PGN32613(this);

            Nano = new clsArduino(this);
            LoadSettings();

            ArdSend32614.ControllerID = (byte)cControllerID;
            ValveCal32615.ControllerID = (byte)cControllerID;

            PauseArea = true;
        }

        public SimType SimulationType
        {
            get { return cSimulationType; }
            set
            {
                cSimulationType = value;
            }
        }

        public int ControllerID
        {
            get { return cControllerID; }
            set
            {
                if (value >= 0 && value < 5)
                {
                    cControllerID = value;
                    ArdSend32614.ControllerID = (byte)cControllerID;
                    ValveCal32615.ControllerID = (byte)cControllerID;
                }
            }
        }

        public int ProductID { get { return cProductID; } }

        public string Units()
        {
            string s = mf.QuantityAbbr[QuantityUnits] + " / " + mf.CoverageAbbr[CoverageUnits];
            return s;
        }

        public string CoverageDescription()
        {
            return mf.CoverageDescriptions[CoverageUnits] + "s";
        }

        public string ProductName
        {
            get { return cProductName; }
            set
            {
                if (value.Length > 15)
                {
                    cProductName = value.Substring(0, 15);
                }
                else if (value.Length == 0)
                {
                    cProductName = "Product " + (ProductID+1).ToString();
                }
                else
                {
                    cProductName = value;
                }
            }
        }


        public string AverageRate()
        {
            if (ArduinoConnected & mf.AOG.Connected()
                & HectaresPerMinute > 0 & Coverage > 0)
            {
                return (QuantityApplied / Coverage).ToString("N1");
            }
            else
            {
                return "0.0";
            }
        }

        public bool CommFromArduino(string sentence, bool RealNano = true)
        {
            bool Result = false;    // return true if there is good comm
            try
            {
                int end = sentence.IndexOf("\r");
                sentence = sentence.Substring(0, end);
                string[] words = sentence.Split(',');

                if (RealNano & SimulationType == SimType.VirtualNano)
                {
                    // block PGN32613 from real nano when simulation is with virtual nano
                    // do nothing
                }
                else
                {
                    if (ArdRec32613.ParseStringData(words))
                    {
                        UpdateQuantity(ArdRec32613.AccumulatedQuantity());
                        ArduinoReceiveTime = DateTime.Now;
                        Result = true;
                    }
                }

                if (Switches32761.ParseStringData(words))
                {
                    ArduinoReceiveTime = DateTime.Now;
                    Result = true;
                }
            }
            catch (Exception)
            {
                return false;
            }
            return Result;
        }

        public string CurrentApplied()
        {
            return QuantityApplied.ToString("N0");
        }

        public string CurrentCoverage()
        {
            return Coverage.ToString("N1");
        }

        public string CurrentRate()
        {
            if (ArduinoConnected & mf.AOG.Connected() & HectaresPerMinute > 0)
            {
                return RateApplied().ToString("N1");
            }
            else
            {
                return "0.0";
            }
        }

        public string SmoothRate()
        {
            if (ArduinoConnected & mf.AOG.Connected() & HectaresPerMinute > 0)
            {
                if (RateSet > 0)
                {
                    double Rt = RateApplied() / RateSet;

                    if (Rt >= .9 & Rt <= 1.1)
                    {
                        return RateSet.ToString("N1");
                    }
                    else
                    {
                        return RateApplied().ToString("N1");
                    }
                }
                else
                {
                    return RateApplied().ToString("N1");
                }
            }
            else
            {
                return "0.0";
            }
        }

        public double CurrentTankRemaining()
        {
            return TankRemaining;
        }

        public double PWM()
        {
            return ArdRec32613.PWMsetting();
        }

        public double RateApplied()
        {
            if (HectaresPerMinute > 0)
            {
                double V = 0;
                switch (CoverageUnits)
                {
                    case 0:
                        // acres
                        V = ArdRec32613.UPM() / (HectaresPerMinute * 2.47);
                        break;

                    case 1:
                        // hectares
                        V = ArdRec32613.UPM() / HectaresPerMinute;
                        break;

                    case 2:
                        // minutes
                        V = ArdRec32613.UPM();
                        break;

                    default:
                        // hours
                        V = ArdRec32613.UPM() * 60;
                        break;
                }
                return V;
            }
            else
            {
                return 0;
            }
        }

        public void ResetApplied()
        {
            QuantityApplied = 0;
            EraseApplied = true;
        }

        public void ResetCoverage()
        {
            Coverage = 0;
            LastWorkedArea = mf.AOG.WorkedArea();
            LastTime = DateTime.Now;
        }

        public void ResetTank()
        {
            TankRemaining = TankSize;
        }

        public void LoadSettings()
        {
            double.TryParse(mf.Tls.LoadProperty("Coverage" + ProductID.ToString()), out Coverage);
            byte.TryParse(mf.Tls.LoadProperty("CoverageUnits" + ProductID.ToString()), out CoverageUnits);
            double.TryParse(mf.Tls.LoadProperty("LastWorkedArea" + ProductID.ToString()), out LastWorkedArea);

            double.TryParse(mf.Tls.LoadProperty("TankRemaining" + ProductID.ToString()), out TankRemaining);
            double.TryParse(mf.Tls.LoadProperty("QuantityApplied" + ProductID.ToString()), out QuantityApplied);
            byte.TryParse(mf.Tls.LoadProperty("QuantityUnits" + ProductID.ToString()), out QuantityUnits);
            double.TryParse(mf.Tls.LoadProperty("LastAccQuantity" + ProductID.ToString()), out LastAccQuantity);

            double.TryParse(mf.Tls.LoadProperty("RateSet" + ProductID.ToString()), out RateSet);
            double.TryParse(mf.Tls.LoadProperty("FlowCal" + ProductID.ToString()), out FlowCal);
            double.TryParse(mf.Tls.LoadProperty("TankSize" + ProductID.ToString()), out TankSize);
            byte.TryParse(mf.Tls.LoadProperty("ValveType" + ProductID.ToString()), out ValveType);
            Enum.TryParse(mf.Tls.LoadProperty("cSimulationType" + ProductID.ToString()), true, out cSimulationType);

            int.TryParse(mf.Tls.LoadProperty("VCN" + ProductID.ToString()), out ValveCal32615.VCN);
            int.TryParse(mf.Tls.LoadProperty("SendTime" + ProductID.ToString()), out ValveCal32615.SendTime);
            int.TryParse(mf.Tls.LoadProperty("WaitTime" + ProductID.ToString()), out ValveCal32615.WaitTime);

            cProductName = mf.Tls.LoadProperty("ProductName" + ProductID.ToString());
            byte.TryParse(mf.Tls.LoadProperty("MinPWM" + ProductID.ToString()), out ValveCal32615.MinPWM);
            int.TryParse(mf.Tls.LoadProperty("ControllerID" + ProductID.ToString()), out cControllerID);
        }

        public void SaveSettings()
        {
            mf.Tls.SaveProperty("Coverage" + ProductID.ToString(), Coverage.ToString());
            mf.Tls.SaveProperty("CoverageUnits" + ProductID.ToString(), CoverageUnits.ToString());
            mf.Tls.SaveProperty("LastWorkedArea" + ProductID.ToString(), LastWorkedArea.ToString());

            mf.Tls.SaveProperty("TankRemaining" + ProductID.ToString(), TankRemaining.ToString());
            mf.Tls.SaveProperty("QuantityApplied" + ProductID.ToString(), QuantityApplied.ToString());
            mf.Tls.SaveProperty("QuantityUnits" + ProductID.ToString(), QuantityUnits.ToString());
            mf.Tls.SaveProperty("LastAccQuantity" + ProductID.ToString(), LastAccQuantity.ToString());

            mf.Tls.SaveProperty("RateSet" + ProductID.ToString(), RateSet.ToString());
            mf.Tls.SaveProperty("FlowCal" + ProductID.ToString(), FlowCal.ToString());
            mf.Tls.SaveProperty("TankSize" + ProductID.ToString(), TankSize.ToString());
            mf.Tls.SaveProperty("ValveType" + ProductID.ToString(), ValveType.ToString());
            mf.Tls.SaveProperty("cSimulationType" + ProductID.ToString(), cSimulationType.ToString());

            mf.Tls.SaveProperty("VCN" + ProductID.ToString(), ValveCal32615.VCN.ToString());
            mf.Tls.SaveProperty("SendTime" + ProductID.ToString(), ValveCal32615.SendTime.ToString());
            mf.Tls.SaveProperty("WaitTime" + ProductID.ToString(), ValveCal32615.WaitTime.ToString());

            mf.Tls.SaveProperty("ProductName" + ProductID.ToString(), cProductName);
            mf.Tls.SaveProperty("MinPWM" + ProductID.ToString(), ValveCal32615.MinPWM.ToString());
            mf.Tls.SaveProperty("ControllerID" + ProductID.ToString(), cControllerID.ToString());
        }

        public byte SectionHi()
        {
            return mf.AOG.SectionControlByteHi;
        }

        public byte SectionLo()
        {
            return mf.AOG.SectionControlByteLo;
        }

        public void SetTankRemaining(double Remaining)
        {
            if (Remaining > 0 && Remaining <= 100000)
            {
                TankRemaining = Remaining;
            }
        }

        public double Speed()
        {
            if (CoverageUnits == 0)
            {
                return mf.AOG.Speed() * .621371;
            }
            else
            {
                return mf.AOG.Speed();
            }
        }

        public void UDPcommFromArduino(byte[] data)
        {
            try
            {
                if (SimulationType != SimType.VirtualNano)  // block PGN32613 from real nano when simulation is with virtual nano
                {
                    if (ArdRec32613.ParseByteData(data))
                    {
                        UpdateQuantity(ArdRec32613.AccumulatedQuantity());
                        ArduinoReceiveTime = DateTime.Now;
                    }
                }

                if (Switches32761.ParseByteData(data))
                {
                    ArduinoReceiveTime = DateTime.Now;
                }
            }
            catch (Exception)
            {
            }
        }

        public void Update()
        {
            StartTime = DateTime.Now;
            CurrentMinutes = (StartTime - LastTime).TotalMinutes;
            if (CurrentMinutes < 0) CurrentMinutes = 0;
            LastTime = StartTime;

            // check connection
            ArduinoConnected = ((StartTime - ArduinoReceiveTime).TotalSeconds < 4);

            if (ArduinoConnected & mf.AOG.Connected())
            {
                // still connected

                // worked area
                TotalWorkedArea = mf.AOG.WorkedArea(); // hectares

                if (PauseArea | (LastWorkedArea > TotalWorkedArea))
                {
                    // exclude area worked while paused or reset LastWorkedArea
                    LastWorkedArea = TotalWorkedArea;
                    PauseArea = false;
                }
                CurrentWorkedArea = TotalWorkedArea - LastWorkedArea;
                LastWorkedArea = TotalWorkedArea;

                // work rate
                CurrentWidth = mf.AOG.WorkingWidth();

                HectaresPerMinute = CurrentWidth * mf.AOG.Speed() * 0.1 / 60.0;

                //coverage
                if (HectaresPerMinute > 0)    // Is application on?
                {
                    switch (CoverageUnits)
                    {
                        case 0:
                            // acres
                            Coverage += CurrentWorkedArea * 2.47105;
                            break;

                        case 1:
                            // hectares
                            Coverage += CurrentWorkedArea;
                            break;

                        case 2:
                            // minutes
                            Coverage += CurrentMinutes;
                            break;

                        default:
                            // hours
                            Coverage += CurrentMinutes / 60;
                            break;
                    }
                }
            }
            else
            {
                // connection lost

                PauseArea = true;
            }

            if (ArduinoConnected) RateSet = Switches32761.NewRate(RateSet);

            if (mf.AOG.Connected() & ArduinoConnected)
            {
                // send to arduino
                ArdSend32614.Send();
                ValveCal32615.Send();

                // send comm to AOG
                Switches32761.Send();
            }
        }

        public double UPMapplied()
        {
            return ArdRec32613.UPM();
        }

        public double TargetUPM() // returns units per minute set rate
        {
            double V = 0;
            switch (CoverageUnits)
            {
                case 0:
                    // acres
                    V = RateSet * HectaresPerMinute * 2.47;
                    break;

                case 1:
                    // hectares
                    V = RateSet * HectaresPerMinute;
                    break;

                case 2:
                    // minutes
                    V = RateSet;
                    break;

                default:
                    // hours
                    V = RateSet / 60;
                    break;
            }
            return V;
        }

        public double Width()
        {
            if (CoverageUnits == 0)
            {
                return CurrentWidth * 3.28;
            }
            else
            {
                return CurrentWidth;
            }
        }

        public double WorkRate()
        {
            if (CoverageUnits == 0)
            {
                return HectaresPerMinute * 2.47105 * 60;
            }
            else
            {
                return HectaresPerMinute * 60;
            }
        }

        private bool QuantityValid(double CurrentDifference)
        {
            bool Result = true;
            try
            {
                // check quantity error
                if (LastQuantityDifference > 0)
                {
                    Ratio = CurrentDifference / LastQuantityDifference;
                    if ((Ratio > 4) | (Ratio < 0.25))
                    {
                        mf.Tls.WriteActivityLog("Quantity Check Ratio: " + Ratio.ToString("N2")
                            + " Current Amount: " + CurrentDifference.ToString("N2") + " Last Amount: " + LastQuantityDifference.ToString("N2")
                            + " RateSet: " + RateSet.ToString("N2") + " Current Rate: " + CurrentRate() + "\n");
                    }
                    if (Ratio > 10) Result = false; // too much of a change in quantity
                }

                // check rate error
                if (RateSet > 0)
                {
                    Ratio = RateApplied() / RateSet;
                    if ((Ratio > 1.5) | (Ratio < 0.67))
                    {
                        mf.Tls.WriteActivityLog("Rate Check Ratio: " + Ratio.ToString("N2")
                            + " Current Amount: " + CurrentDifference.ToString("N2") + " Last Amount: " + LastQuantityDifference.ToString("N2")
                            + " RateSet: " + RateSet.ToString("N2") + " Current Rate: " + CurrentRate() + "\n");
                    }
                }

                // record values periodically
                if ((DateTime.Now - QcheckLast).TotalMinutes > 30)
                {
                    QcheckLast = DateTime.Now;

                    if (LastQuantityDifference > 0)
                    {
                        Ratio = CurrentDifference / LastQuantityDifference;
                        mf.Tls.WriteActivityLog("Quantity Check Ratio: " + Ratio.ToString("N2")
                            + " Current Amount: " + CurrentDifference.ToString("N2") + " Last Amount: " + LastQuantityDifference.ToString("N2")
                            + " RateSet: " + RateSet.ToString("N2") + " Current Rate: " + CurrentRate() + "\n");
                    }
                }

                LastQuantityDifference = CurrentDifference;
                return Result;
            }
            catch (Exception ex)
            {
                mf.Tls.WriteErrorLog("cRateCals: QuantityValid: " + ex.Message);
                return false;
            }
        }

        private void UpdateQuantity(double AccQuantity)
        {
            if (AccQuantity > LastAccQuantity)
            {
                CurrentQuantity = AccQuantity - LastAccQuantity;
                LastAccQuantity = AccQuantity;

                if (QuantityValid(CurrentQuantity))
                {
                    // tank remaining
                    TankRemaining -= CurrentQuantity;

                    // quantity applied
                    QuantityApplied += CurrentQuantity;
                }
            }
            else
            {
                // reset
                LastAccQuantity = AccQuantity;
            }
        }
    }
}