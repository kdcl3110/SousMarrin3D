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
	/// This interface can used to get motion data from application or game.
	///
	/// When the ForceSeatMI telemetry is implemented in application, any motion software is able to gather simulation data and
	/// control motion platform (2DOF, 3DOF or 6DOF). This structure should be filled by the application and sent to motion processing
	/// system. It is require to fill "structSize" and "mask", other fields are OPTIONAL. It means that the application does not
	/// have to support or provide all parameters mentioned below, but it is good to provide as much as possible.
	///
	/// DEPRECATED: For new applications use FSMI_TelemetryACE.
	///
	/// FIELDS LEGEND:
	/// ==============
	/// All values are in local vehicle coordinates.
	///
	///   YAW   - rotation along vertical axis,
	///          > 0 when vehicle front is rotating right,
	///          < 0 when vehicle front is rotating left
	///   PITCH - rotation along lateral axis,
	///          > 0 when vehicle front is rotating up
	///          > 0 when vehicle front is rotating down
	///   ROLL  - rotation along longitudinal axis,
	///          > 0 when vehicle highest point is rotating right,
	///          < 0 when vehicle highest point is rotating left
	///   SWAY  - transition along lateral axis,
	///          > 0 when vehicle is moving right,
	///          < 0 when vehicle is moving left
	///   HEAVE - transition along vertical axis,
	///          > 0 when vehicle is moving up,
	///          < 0 when vehicle is moving down
	///   SURGE - transition along longitudinal axis,
	///          > 0 when vehicle is moving forward,
	///          < 0 when vehicle is moving backward
	///
	/// Please check below link for details description of yaw, pitch and roll:
	/// http://en.wikipedia.org/wiki/Ship_motions
	/// </summary>
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct FSMI_Telemetry
	{
		/// <summary>
		/// Put here sizeof(FSMI_Telemetry).
		/// NOTE: This field is mandatory.
		/// </summary>
		public byte structSize;

		/// <summary>
		/// Set bits in the mask to tell the motion software which fields in this stucture are provided by your application.
		/// </summary>
		public uint mask;

		/// <summary>
		/// Only single bit is used in current version.
		///  (state & 0x01) == 0 -> no pause
		///  (state & 0x01) == 1 -> paused
		/// NOTE: If the field is provided, set FSMI_TEL_BIT.STATE in the mask.
		/// </summary>
		public byte state;

		/// <summary>
		/// Engine RPM.
		/// NOTE: If the field is provided, set FSMI_TEL_BIT.RPM in the mask.
		/// </summary>
		public uint rpm;

		/// <summary>
		/// Vehicle speed in m/s, can be < 0 for reverse.
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.SPEED in the mask.
		/// </summary>
		public float speed;

		/// <summary>
		/// yaw in radians
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.YAW_PITCH_ROLL in the mask.
		/// </summary>
		public float yaw;

		/// <summary>
		/// vehicle pitch in radians
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.YAW_PITCH_ROLL in the mask.
		/// </summary>
		public float pitch;

		/// <summary>
		/// vehicle roll in radians
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.YAW_PITCH_ROLL in the mask.
		/// </summary>
		public float roll;

		/// <summary>
		/// angular acceleration in radians/s^2
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.YAW_PITCH_ROLL_ACCELERATION in the mask.
		/// </summary>
		public float yawAcceleration;

		/// <summary>
		/// angular acceleration in radians/s^2
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.YAW_PITCH_ROLL_ACCELERATION in the mask.
		/// </summary>
		public float pitchAcceleration;

		/// <summary>
		/// angular acceleration in radians/s^2
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.YAW_PITCH_ROLL_ACCELERATION in the mask.
		/// </summary>
		public float rollAcceleration;

		/// <summary>
		/// angular velocity in radians/s
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.YAW_PITCH_ROLL_SPEED in the mask.
		/// </summary>
		public float yawSpeed;

		/// <summary>
		/// angular velocity in radians/s
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.YAW_PITCH_ROLL_SPEED in the mask.
		/// </summary>
		public float pitchSpeed;

		/// <summary>
		/// angular velocity in radians/s
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.YAW_PITCH_ROLL_SPEED in the mask.
		/// </summary>
		public float rollSpeed;

		/// <summary>
		/// sway acceleration in m/s^2
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.SWAY_HEAVE_SURGE_ACCELERATION in the mask.
		/// </summary>
		public float swayAcceleration;

		/// <summary>
		/// heave acceleration in m/s^2
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.SWAY_HEAVE_SURGE_ACCELERATION in the mask.
		/// </summary>
		public float heaveAcceleration;

		/// <summary>
		/// surge acceleration in m/s^2
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.SWAY_HEAVE_SURGE_ACCELERATION in the mask.
		/// </summary>
		public float surgeAcceleration;

		/// <summary>
		/// sway velocity in m/s
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.SWAY_HEAVE_SURGE_SPEED in the mask.
		/// </summary>
		public float swaySpeed;

		/// <summary>
		/// heave velocity in m/s
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.SWAY_HEAVE_SURGE_SPEED in the mask.
		/// </summary>
		public float heaveSpeed;

		/// <summary>
		/// surge velocity in m/s
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.SWAY_HEAVE_SURGE_SPEED in the mask.
		/// </summary>
		public float surgeSpeed;

		/// <summary>
		/// 0 to 100 (in percent)
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.PEDALS_POSITION in the mask.
		/// </summary>
		public byte gasPedalPosition;

		/// <summary>
		/// 0 to 100 (in percent)
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.PEDALS_POSITION in the mask.
		/// </summary>
		public byte brakePedalPosition;

		/// <summary>
		/// 0 to 100 (in percent)
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.PEDALS_POSITION in the mask.
		/// </summary>
		public byte clutchPedalPosition;

		/// <summary>
		/// Current gear number, -1 for reverse, 0 for neutral, 1, 2, 3, ...
		/// NOTE: If the field is provided, set FSMI_TEL_BIT.GEAR_NUMBER in the mask.
		/// </summary>
		public sbyte gearNumber;

		/// <summary>
		/// Ground type, e.g. grass, dirt, gravel, please check FSMI_GroundType for details.
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.GROUND_TYPE in the mask.
		/// </summary>
		public byte leftSideGroundType;

		/// <summary>
		/// Ground type, e.g. grass, dirt, gravel, please check FSMI_GroundType for details.
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.GROUND_TYPE in the mask.
		/// </summary>
		public byte rightSideGroundType;

		/// <summary>
		/// DEPRECATED: Do not use, kept only for backward compatibility
		/// in Newtons (N), zero when there is no collision
		/// </summary>
		public float collisionForce;

		/// <summary>
		/// DEPRECATED: Do not use, kept only for backward compatibility
		/// yaw for start point of collision force vector, end point is always in vehicle center
		/// </summary>
		public float collisionYaw;

		/// <summary>
		/// DEPRECATED: Do not use, kept only for backward compatibility
		/// pitch for start point of collision force vector, end point is always in vehicle center
		/// </summary>
		public float collisionPitch;

		/// <summary>
		/// DEPRECATED: Do not use, kept only for backward compatibility
		/// roll for start point of collision force vector, end point is always in vehicle center
		/// </summary>
		public float collisionRoll;

		/// <summary>
		/// Global position, right
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.GLOBAL_POSITION in the mask.
		/// </summary>
		public float globalPositionX;

		/// <summary>
		/// Global position, up
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.GLOBAL_POSITION in the mask.
		/// </summary>
		public float globalPositionY;

		/// <summary>
		/// Global position, forward
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.GLOBAL_POSITION in the mask.
		/// </summary>
		public float globalPositionZ;

		/// <summary>
		/// Simulation time in milliseconds, can be relative (e.g. 0 means means application has just started).
		/// NOTE: If the field is provided, set FSMI_TEL_BIT.TIME in the mask.
		/// </summary>
		public uint timeMs;

		/// <summary>
		/// State of buttons, gun triggers, etc. It is passed directly to the motion platform.
		/// NOTE: If the field is provided, set FSMI_TEL_BIT.TRIGGERS in the mask.
		/// </summary>
		public byte triggers;

		/// <summary>
		/// Engine maximum RPM, it is used to simulate rev limiter.
		/// NOTE: If the field is provided, set FSMI_TEL_BIT.MAX_RPM in the mask.
		/// </summary>
		public uint maxRpm;

		/// <summary>
		/// Combination of FSMI_Flags.
		/// NOTE: If the field is provided, set FSMI_TEL_BIT.FLAGS in the mask.
		/// </summary>
		public uint flags;

		/// <summary>
		/// Custom field that can be used in script in ForceSeatPM to trigger user defined actions.
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.AUX in the mask.
		/// </summary>
		public float aux1;

		/// <summary>
		/// Custom field that can be used in script in ForceSeatPM to trigger user defined actions.
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.AUX in the mask.
		/// </summary>
		public float aux2;

		/// <summary>
		/// Custom field that can be used in script in ForceSeatPM to trigger user defined actions.
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.AUX in the mask.
		/// </summary>
		public float aux3;

		/// <summary>
		/// Custom field that can be used in script in ForceSeatPM to trigger user defined actions.
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.AUX in the mask.
		/// </summary>
		public float aux4;

		/// <summary>
		/// Custom field that can be used in script in ForceSeatPM to trigger user defined actions.
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.AUX in the mask.
		/// </summary>
		public float aux5;

		/// <summary>
		/// Custom field that can be used in script in ForceSeatPM to trigger user defined actions.
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.AUX in the mask.
		/// </summary>
		public float aux6;

		/// <summary>
		/// Custom field that can be used in script in ForceSeatPM to trigger user defined actions.
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.AUX in the mask.
		/// </summary>
		public float aux7;

		/// <summary>
		/// Custom field that can be used in script in ForceSeatPM to trigger user defined actions.
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.AUX in the mask.
		/// </summary>
		public float aux8;

		/// <summary>
		/// Extra values that will be added at the end of signal processing. Those values will NOT be
		/// filtered, smoothed or processed in any way. They might be used to generate additional
		/// vibrations or custom effects.
		/// In radians
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.EXTRA_YAW_PITCH_ROLL_SWAY_HEAVE_SURGE in the mask.
		/// </summary>
		public float extraYaw;

		/// <summary>
		/// Extra values that will be added at the end of signal processing. Those values will NOT be
		/// filtered, smoothed or processed in any way. They might be used to generate additional
		/// vibrations or custom effects.
		/// In radians
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.EXTRA_YAW_PITCH_ROLL_SWAY_HEAVE_SURGE in the mask.
		/// </summary>
		public float extraPitch;

		/// <summary>
		/// Extra values that will be added at the end of signal processing. Those values will NOT be
		/// filtered, smoothed or processed in any way. They might be used to generate additional
		/// vibrations or custom effects.
		/// In radians
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.EXTRA_YAW_PITCH_ROLL_SWAY_HEAVE_SURGE in the mask.
		/// </summary>
		public float extraRoll;

		/// <summary>
		/// Extra values that will be added at the end of signal processing. Those values will NOT be
		/// filtered, smoothed or processed in any way. They might be used to generate additional
		/// vibrations or custom effects.
		/// In mm
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.EXTRA_YAW_PITCH_ROLL_SWAY_HEAVE_SURGE in the mask.
		/// </summary>
		public float extraSway;

		/// <summary>
		/// Extra values that will be added at the end of signal processing. Those values will NOT be
		/// filtered, smoothed or processed in any way. They might be used to generate additional
		/// vibrations or custom effects.
		/// In mm
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.EXTRA_YAW_PITCH_ROLL_SWAY_HEAVE_SURGE in the mask.
		/// </summary>
		public float extraHeave;

		/// <summary>
		/// Extra values that will be added at the end of signal processing. Those values will NOT be
		/// filtered, smoothed or processed in any way. They might be used to generate additional
		/// vibrations or custom effects.
		/// In mm
		/// NOTE: If this field is provided, set FSMI_TEL_BIT.EXTRA_YAW_PITCH_ROLL_SWAY_HEAVE_SURGE in the mask.
		/// </summary>
		public float extraSurge;
	}

	public enum FSMI_GroundType
	{
		UknownGround      = 0,
		TarmacGround      = 1,
		GrassGround       = 2,
		DirtGround        = 3,
		GravelGround      = 4,
		RumbleStripGround = 5
	}

	public struct FSMI_Flags
	{
		public const int ShiftLight       = (1 << 0);
		public const int AbsIsWorking     = (1 << 1);
		public const int EspIsWorking     = (1 << 2);
		public const int FrontWheelDrive  = (1 << 3);
		public const int RearWheelDrive   = (1 << 4);
	}

	public struct FSMI_TEL_BIT
	{
		public const int STATE                                              = (1 << 1);
		public const int RPM                                                = (1 << 2);
		public const int SPEED                                              = (1 << 3);

		public const int YAW_PITCH_ROLL                                     = (1 << 4);
		public const int YAW_PITCH_ROLL_ACCELERATION                        = (1 << 5);
		public const int YAW_PITCH_ROLL_SPEED                               = (1 << 6);
		public const int SWAY_HEAVE_SURGE_ACCELERATION                      = (1 << 7);
		public const int SWAY_HEAVE_SURGE_SPEED                             = (1 << 8);

		public const int PEDALS_POSITION                                    = (1 << 9);
		public const int GEAR_NUMBER                                        = (1 << 10);
		public const int GROUND_TYPE                                        = (1 << 11);
		public const int COLLISION                                          = (1 << 12);

		public const int GLOBAL_POSITION                                    = (1 << 13);
		public const int TIME                                               = (1 << 14);
		public const int TRIGGERS                                           = (1 << 15);

		public const int MAX_RPM                                            = (1 << 16);
		public const int FLAGS                                              = (1 << 17);
		public const int AUX                                                = (1 << 18);
		public const int EXTRA_YAW_PITCH_ROLL_SWAY_HEAVE_SURGE              = (1 << 19);
	}
}
