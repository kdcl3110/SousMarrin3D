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
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct FSMI_TelemetryRUF
	{
		/// <summary>
		/// + is right, - is left
		/// </summary>
		public float right;

		/// <summary>
		/// + is upward, - is downward
		/// </summary>
		public float upward;

		/// <summary>
		/// + is forward, - is backward
		/// </summary>
		public float forward;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct FSMI_TelemetryPRY
	{
		/// <summary>
		/// + front goes up, - front goes down
		/// </summary>
		public float pitch;

		/// <summary>
		/// + right side goes down, - right sidegoes up
		/// </summary>
		public float roll;

		/// <summary>
		/// + front goes left, - front goes right
		/// </summary>
		public float yaw;
	}

	/// <summary>
	/// This structure is used to forward vehicle physics data from application/game to the ForceSeatPM
	/// where the data is next processed and transformed into motion platform top table's movements.
	/// </summary>
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct FSMI_TelemetryACE
	{
		/// <summary>
		/// Put here sizeof(FSMI_TelemetryACE).
		/// MANDATORY: Yes
		/// </summary>
		public byte structSize;

		/// <summary>
		/// Only single bit is used in current version.
		///  (state & 0x01) == 0 -> no pause
		///  (state & 0x01) == 1 -> paused, FSMI_STATE_PAUSE
		/// </summary>
		public byte state;

		/// <summary>
		/// Current gear number, -1 for reverse, 0 for neutral, 1, 2, 3, ...
		/// </summary>
		public sbyte gearNumber;

		/// <summary>
		/// 0 to 100 (in percent)
		/// </summary>
		public byte accelerationPedalPosition;

		/// <summary>
		/// 0 to 100 (in percent)
		/// </summary>
		public byte brakePedalPosition;

		/// <summary>
		/// 0 to 100 (in percent)
		/// </summary>
		public byte clutchPedalPosition;

		public byte dummy1;
		public byte dummy2;

		/// <summary>
		/// Engine RPM
		/// </summary>
		public uint rpm;

		/// <summary>
		/// Engine max RPM
		/// </summary>
		public uint maxRpm;

		/// <summary>
		/// Forward speed, in [m/s]. For dashboard applications.
		/// </summary>
		public float vehicleForwardSpeed;

		/// <summary>
		/// Lateral, vertical and longitudinal acceleration from the simulation.
		/// UNITS: [m/s^2]
		/// MANDATORY: Yes
		/// </summary>
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
		public FSMI_TelemetryRUF[] bodyLinearAcceleration;

		/// <summary>
		/// Angular rotation velocity about vertical, lateral and longitudinal axes.
		/// UNITS: [radians/s]
		/// MANDATORY: Yes
		/// </summary>
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
		public FSMI_TelemetryPRY[] bodyAngularVelocity;

		/// <summary>
		/// Position of the user head for accelerations and angular velocity effects generation
		/// in relation to the top frame center.
		/// UNITS: [meters]
		/// </summary>
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
		public FSMI_TelemetryRUF[] headPosition;

		/// <summary>
		/// Additional translation for the top frame.
		/// UNITS: [meters]
		/// </summary>
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
		public FSMI_TelemetryRUF[] extraTranslation;

		/// <summary>
		/// Additional rotation for the top frame.
		/// UNITS: [rad]
		/// </summary>
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
		public FSMI_TelemetryPRY[] extraRotation;

		/// <summary>
		/// Rotation center for the 'extra' transformation in relation to the top frame center.
		/// UNITS: [meters]
		/// </summary>
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
		public FSMI_TelemetryRUF[] extraRotationCenter;

		/// <summary>
		/// Custom field that can be used in script in ForceSeatPM to trigger user defined actions.
		/// </summary>
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)FSMI_Defines.UserAuxCount)]
		public float[] userAux;

		/// <summary>
		/// Custom field that can be used in script in ForceSeatPM to trigger user defined actions.
		/// </summary>
		public uint userFlags;

		/// <summary>
		/// Lateral, vertical and longitudinal velocity from the simulation.
		/// UNITS: [m/s^2]
		/// MANDATORY: Yes
		/// </summary>
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
		public FSMI_TelemetryRUF[] bodyLinearVelocity;

		/// <summary>
		/// Additional body pitch that can be used to simulate uphills and downhills.
		/// This field is being used by ACE and therefore can be fully filtered and tuned.
		/// </summary>
		public float bodyPitch;

		/// <summary>
		/// Additional body roll
		/// This field is being used by ACE and therefore can be fully filtered and tuned.
		/// </summary>
		public float bodyRoll;

		/// <summary>
		/// Optional field to provide road harshness in case bodyLinearAcceleration.upward is not sufficient
		/// </summary>
		public float roadHarshness;

		/// <summary>
		/// Optional field to provide sidelip data in case calculation from linear velocities in not sufficient
		/// </summary>
		public float sideSlip;

		/// <summary>
		/// Optional field to provide g (for planes) in case vertical acceleration is not sufficient to simulate high G maneuvers
		/// </summary>
		public float g;

		/// <summary>
		/// Helper method to create a structure with preallocated space for nested structures
		/// </summary>
		public static FSMI_TelemetryACE Prepare()
		{
			var telemetry                    = new FSMI_TelemetryACE();
			telemetry.structSize             = (byte)Marshal.SizeOf(telemetry);
			telemetry.bodyLinearAcceleration = new FSMI_TelemetryRUF[1];
			telemetry.bodyAngularVelocity    = new FSMI_TelemetryPRY[1];
			telemetry.headPosition           = new FSMI_TelemetryRUF[1];
			telemetry.extraTranslation       = new FSMI_TelemetryRUF[1];
			telemetry.extraRotation          = new FSMI_TelemetryPRY[1];
			telemetry.extraRotationCenter    = new FSMI_TelemetryRUF[1];
			telemetry.userAux                = new float[(int)FSMI_Defines.UserAuxCount];
			telemetry.bodyLinearVelocity     = new FSMI_TelemetryRUF[1];

			return telemetry;
		}
	}
}
