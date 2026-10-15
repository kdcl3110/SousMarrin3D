/*
 * Copyright (C) 2012-2026 MotionSystems
 *
 * This file is part of ForceSeatMI SDK.
 *
 * www.motionsystems.eu
 *
 */

using System;
using System.Runtime.InteropServices;

namespace MotionSystems
{
	/// <summary>
	/// List of possible motion platform working states
	///
	/// Only one park mode is valid at given time.
	/// Greater parking mode value (number) = greater priority.
	/// It means that transport parking wins over normal parking
	/// and normal parking wins over parking to center.
	/// </summary>
	public struct FSMI_PlatformCurrentState
	{
		public const int AnyPaused                = 1 << 0; // 0b00000001
		public const int ParkingCompleted         = 1 << 1; // 0b00000010
		public const int Offline                  = 1 << 2; // 0b00000100

		public const int RefRunCompleted          = 1 << 4; // 0b00010000

		/// <summary>
		/// Only one park mode is valid at given time.
		/// Greater parking mode value (number) = greater priority.
		/// It means that transport parking wins over normal parking
		/// and normal parking wins over parking to center.
		/// </summary>
		public const int ParkModeMask             = 0xE0;   // 0b11100000
		public const int NoParking                = 0x00;   // 0b00000000
		public const int SoftParkToCenter         = 0x20;   // 0b00100000
		public const int SoftParkNormal           = 0x40;   // 0b01000000
		public const int SoftParkForTransport     = 0x60;   // 0b01100000
		public const int OperatorParkToCenter     = 0x80;   // 0b10000000
		public const int OperatorParkNormal       = 0xA0;   // 0b10100000
		public const int OperatorParkForTransport = 0xC0;   // 0b11000000
		public const int Reserved                 = 0xE0;   // 0b11100000
	};

	/// <summary>
	/// Actual platform status and motors position
	/// </summary>
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct FSMI_PlatformInfo
	{
		/// <summary>
		/// check if this equals to sizeof(FSMI_PlatformInfo)\
		/// </summary>
		public byte structSize;

		/// <summary>
		/// System ticks, 64-bits
		/// </summary>
		public ulong timemark;

		public byte isConnected;

		/// <summary>
		/// bits from FSMI_PlatformCurrentState
		/// </summary>
		public byte state;

		public ushort actualMotor1_Position;
		public ushort actualMotor2_Position;
		public ushort actualMotor3_Position;
		public ushort actualMotor4_Position;
		public ushort actualMotor5_Position;
		public ushort actualMotor6_Position;
		public int actualMotor1_Speed;
		public int actualMotor2_Speed;
		public int actualMotor3_Speed;
		public int actualMotor4_Speed;
		public int actualMotor5_Speed;
		public int actualMotor6_Speed;

		/// <summary>
		/// DEPRECATED: this field is always empty
		/// </summary>
		public byte isThermalProtectionActivated;

		/// <summary>
		/// worst module (actuator or node) status - numerical code
		/// </summary>
		public byte worstModuleStatusCode;

		/// <summary>
		/// index of module that above status applies to
		/// </summary>
		public byte worstModuleStatusIndex;

		/// <summary>
		/// DEPRECATED: this field is always empty
		/// </summary>
		public byte coolingSystemMalfunction;

		/// <summary>
		/// true if Inverse and Forward Kinematics are supported
		/// </summary>
		public byte isKinematicsSupported;

		/// <summary>
		/// DEPRECATED: this field is always empty
		/// </summary>
		public float ikPrecision1;

		/// <summary>
		/// DEPRECATED: this field is always empty
		/// </summary>
		public float ikPrecision2;

		/// <summary>
		/// DEPRECATED: this field is always empty
		/// </summary>
		public float ikPrecision3;

		/// <summary>
		/// DEPRECATED: this field is always empty
		/// </summary>
		public float ikPrecision4;

		/// <summary>
		/// DEPRECATED: this field is always empty
		/// </summary>
		public float ikPrecision5;

		/// <summary>
		/// DEPRECATED: this field is always empty
		/// </summary>
		public float ikPrecision6;

		/// <summary>
		/// true if Inverse Kinematics was calculated correctly and given position is withing operating range
		/// </summary>
		public byte ikRecentStatus;

		/// <summary>
		/// roll in rad from Fordward Kinematics
		/// </summary>
		public float fkRoll;

		/// <summary>
		/// pitch in rad from Fordward Kinematics
		/// </summary>
		public float fkPitch;

		/// <summary>
		/// yaw in rad from Fordward Kinematics
		/// </summary>
		public float fkYaw;

		/// <summary>
		/// heave in mm from Fordward Kinematics
		/// </summary>
		public float fkHeave;

		/// <summary>
		/// sway in mm from Fordward Kinematics
		/// </summary>
		public float fkSway;

		/// <summary>
		/// surge in mm from Fordward Kinematics
		/// </summary>
		public float fkSurge;

		/// <summary>
		/// true if Fordward Kinematics was calculated correctly
		/// </summary>
		public byte fkRecentStatus;

		public ushort requiredMotor1_Position;
		public ushort requiredMotor2_Position;
		public ushort requiredMotor3_Position;
		public ushort requiredMotor4_Position;
		public ushort requiredMotor5_Position;
		public ushort requiredMotor6_Position;

		/// <summary>
		/// roll in deg from Forward Kinematics
		/// </summary>
		public float fkRoll_deg;

		/// <summary>
		/// pitch in deg from Forward Kinematics
		/// </summary>
		public float fkPitch_deg;

		/// <summary>
		/// yaw in deg from Forward Kinematics
		/// </summary>
		public float fkYaw_deg;

		/// <summary>
		/// roll in deg/s from Forward Kinematics
		/// </summary>
		public float fkRollSpeed_deg;

		/// <summary>
		/// pitch in deg/s from Forward Kinematics
		/// </summary>
		public float fkPitchSpeed_deg;

		/// <summary>
		/// yaw in deg/s from Forward Kinematics
		/// </summary>
		public float fkYawSpeed_deg;

		/// <summary>
		/// heave in mm/s from Forward Kinematics
		/// </summary>
		public float fkHeaveSpeed;

		/// <summary>
		/// sway in mm/s from Forward Kinematics
		/// </summary>
		public float fkSwaySpeed;

		/// <summary>
		/// surge in mm/s from Forward Kinematics
		/// </summary>
		public float fkSurgeSpeed;

		/// <summary>
		/// roll in deg/s2 from Forward Kinematics
		/// </summary>
		public float fkRollAcc_deg;

		/// <summary>
		/// pitch in deg/s2 from Forward Kinematics
		/// </summary>
		public float fkPitchAcc_deg;

		/// <summary>
		/// yaw in deg/s2 from Forward Kinematics
		/// </summary>
		public float fkYawAcc_deg;

		/// <summary>
		/// heave in mm/s2 from Forward Kinematics
		/// </summary>
		public float fkHeaveAcc;

		/// <summary>
		/// sway in mm/s2 from Forward Kinematics
		/// </summary>
		public float fkSwayAcc;

		/// <summary>
		/// surge in mm/s2 from Forward Kinematics
		/// </summary>
		public float fkSurgeAcc;
	}
}
