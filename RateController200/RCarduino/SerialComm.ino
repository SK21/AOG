#if(CommType == 0)

//PGN32613 to Rate Controller from Arduino
//0	HeaderHi		127
//1	HeaderLo		101
//2 Controller ID
//3	rate applied Hi		100 X actual
//4	rate applied Lo
//5	acc.Quantity byte 3		100 X actual
//6	acc.Quantity byte 2
//7	acc.Quantity byte 1
//8 PWM Hi
//9 PWM Lo

//PGN32761 Switches
//0	HeaderHi		127
//1	HeaderLo		249
//2 -
//3 -
//4 -
//5	SecOn Hi		8 - 15
//6	SecOn Lo		0 - 7
//7	SecOff Hi
//8	SecOff Lo
//9	Command
//- bit 0		auto button on
//- bit 1		auto button off
//- bit 2, 3		rate change steps 0 - 3
//- bit 4		0 - change left, 1 - change right
//- bit 5		0 - rate down, 1 - rate up

void SendSerial()
{
	// PGN 32613
	Serial.print(127);	// headerHi
	Serial.print(",");
	Serial.print(101);	// headerLo
	Serial.print(",");

	Serial.print(ControllerID);
	Serial.print(",");

	// rate applied, 100 X actual
	Temp = (int)(FlowRate * 100) >> 8;
	Serial.print(Temp);
	Serial.print(",");
	Temp = (FlowRate * 100);
	Serial.print(Temp);
	Serial.print(",");

	// accumulated quantity, 3 bytes
	long Units = accumulatedCounts * 100.0 / MeterCal;
	Temp = Units >> 16;
	Serial.print(Temp);
	Serial.print(",");
	Temp = Units >> 8;
	Serial.print(Temp);
	Serial.print(",");
	Temp = Units;
	Serial.print(Temp);
	Serial.print(",");

	// pwmSetting
	Temp = (byte)((pwmSetting * 10) >> 8);
	Serial.print(Temp);
	Serial.print(",");

	Temp = (byte)(pwmSetting * 10);
	Serial.print(Temp);

	Serial.println();
	Serial.flush();   // flush out buffer     



	// PGN 32761
	Serial.print(127);	// headerHi
	Serial.print(",");
	Serial.print(249);	// headerLo
	Serial.print(",");

	Serial.print(0);
	Serial.print(",");
	Serial.print(0);
	Serial.print(",");
	Serial.print(0);
	Serial.print(",");

	Serial.print(0);	// Relay Hi
	Serial.print(",");
	Serial.print(RelayToAOG);	// Relay Lo
	Serial.print(",");

	Serial.print(SecSwOff[1]);
	Serial.print(",");
	Serial.print(SecSwOff[0]);
	Serial.print(",");

	Serial.print(OutCommand);

	Serial.println();
	Serial.flush();   // flush out buffer     
}

//PGN32614 to Arduino from Rate Controller
//0	HeaderHi		127
//1	HeaderLo		102
//2 Controller ID
//3	relay Hi		8 - 15
//4	relay Lo		0 - 7
//5	rate set Hi		100 X actual
//6	rate set Lo		100 X actual
//7	Flow Cal Hi		100 X actual
//8	Flow Cal Lo		100 X actual
//9	Command
//- bit 0		reset acc.Quantity
//- bit 1, 2		valve type 0 - 3
//- bit 3		simulate flow
//
//PGN32615 to Arduino from Rate Controller
// 0 HeaderHi       127
// 1 HeaderLo       103
// 2 Controller ID
// 3 VCN Hi         Valve Cal Number
// 4 VCN Lo
// 5 Send Hi        ms sending pwm
// 6 Send Lo
// 7 Wait Hi        ms to wait before sending pwm again
// 8 Wait Lo
// 9 Min PWM

void ReceiveSerial()
{
	if (Serial.available() > 0 && !PGN32614Found && !PGN32615Found) //find the header, 
	{
		UnSignedTemp = Serial.read();
		header = tempHeader << 8 | UnSignedTemp;               //high,low bytes to make int
		tempHeader = UnSignedTemp;                             //save for next time
		PGN32614Found = (header == 32614);
		PGN32615Found = (header == 32615);
	}
	if (Serial.available() > 7)
	{
		if (PGN32614Found)
		{
			PGN32614Found = false;
			byte ConID = Serial.read();
			if (ConID == ControllerID)
			{
				RelayHi = Serial.read();
				RelayFromAOG = Serial.read();

				// rate setting, 100 times actual
				UnSignedTemp = Serial.read() << 8 | Serial.read();
				rateSetPoint = (float)(UnSignedTemp * 0.01);

				//Meter Cal, 100 times actual
				UnSignedTemp = Serial.read() << 8 | Serial.read();
				MeterCal = (float)(UnSignedTemp * 0.01);

				// command byte
				InCommand = Serial.read();
				if ((InCommand & 1) == 1) accumulatedCounts = 0;	// reset accumulated count

				ValveType = 0;
				if ((InCommand & 2) == 2) ValveType += 1;
				if ((InCommand & 4) == 4) ValveType += 2;

				SimulateFlow = ((InCommand & 8) == 8);

				//reset watchdog as we just heard from AgOpenGPS
				watchdogTimer = 0;
				AOGconnected = true;
			}
		}

		if (PGN32615Found)
		{
			PGN32615Found = false;

			byte ConID = Serial.read();
			if (ConID == ControllerID)
			{
				VCN = Serial.read() << 8 | Serial.read();
				SendTime = Serial.read() << 8 | Serial.read();
				WaitTime = Serial.read() << 8 | Serial.read();
				MinPWMvalue = Serial.read();

				//reset watchdog as we just heard from AgOpenGPS
				watchdogTimer = 0;
				AOGconnected = true;
			}
		}
	}
}
#endif



