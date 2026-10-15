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
	/// This structure defines position of top frame (table) in physical units (rad, mm).
	/// It uses Inverse Kinematics module and it might not be supported by all motion platforms.
	/// By default BestMatch strategy is used.
	/// </summary>
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct FSMI_TopTablePositionPhysical
	{
		/// <summary>
		/// Put here sizeof(FSMI_TopTablePositionPhysical).
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
		/// NOTE: If the field is provided, set FSMI_POS_BIT.STATE in the mask.
		/// </summary>
		public byte state;

		/// <summary>
		/// in radians, roll < 0 = left, roll > 0 = right
		/// NOTE: If this field is provided, set FSMI_POS_BIT.POSITION in the mask.
		/// </summary>

		public float roll;

		/// <summary>
		/// in radians, pitch < 0 = front, pitch > 0 = rear
		/// NOTE: If this field is provided, set FSMI_POS_BIT.POSITION in the mask.
		/// </summary>
		public float pitch;

		/// <summary>
		/// in radians, yaw < 0 = right, yaw > 0 = left
		/// NOTE: If this field is provided, set FSMI_POS_BIT.POSITION in the mask.
		/// </summary>
		public float yaw;

		/// <summary>
		/// in mm, heave < 0 - down, heave > 0 - top
		/// NOTE: If this field is provided, set FSMI_POS_BIT.POSITION in the mask.
		/// </summary>
		public float heave;

		/// <summary>
		/// in mm, sway < 0 - left, sway > 0 - right
		/// NOTE: If this field is provided, set FSMI_POS_BIT.POSITION in the mask.
		/// </summary>
		public float sway;

		/// <summary>
		/// in mm, surge < 0 - rear, surge > 0 - front
		/// NOTE: If this field is provided, set FSMI_POS_BIT.POSITION in the mask.
		/// </summary>
		public float surge;

		/// <summary>
		/// 0 to 65535, actual speed is not always equal to max speed due to ramps.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.MAX_SPEED in the mask.
		/// </summary>
		public ushort maxSpeed;

		/// <summary>
		/// State of buttons, gun triggers, etc. It is passed directly to the motion platform.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.TRIGGERS in the mask.
		/// </summary>
		public byte triggers;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux1;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux2;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux3;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux4;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux5;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux6;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux7;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux8;
	}

	/// <summary>
	/// This structure defines position of top frame (table) in physical units (rad, mm) by specifing transformation matrix.
	/// It uses Inverse Kinematics module and it is dedicated for 6DoF motion platforms.
	/// If matrix transformation is specified, the Inverse Kinematics module always uses FullMatch strategy.
	/// </summary>
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct FSMI_TopTableMatrixPhysical
	{
		/// <summary>
		/// Put here sizeof(FSMI_TopTableMatrixPhysical).
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
		/// NOTE: If the field is provided, set FSMI_POS_BIT.STATE in the mask.
		/// </summary>
		public byte state;

		/// <summary>
		/// 3D transformation matrix
		///
		/// OFFSET (in mm):
		///   x axis = left-right movement, sway,  x < 0 - left, x > 0 - right
		///   y axis = rear-front movement, surge, y < 0 - rear, y > 0 - front
		///   z axis = down-top movement,   heave, z < 0 - down, z > 0 - top
		///
		/// ROTATION (in radians):
		///   x axis, pitch = x < 0 = front, x > 0 = rear
		///   y axis, roll = y < 0  = left,  y > 0 = right
		///   z axis, yaw  = z < 0  = right, z > 0 = left
		///
		/// EXAMPLE:
		///   FSMI_FLOAT sinAX = sinf(pitch), cosAX = cosf(pitch), sinAY = sinf(roll), cosAY = cosf(roll), sinAZ = sinf(yaw), cosAZ = cosf(yaw);
		///   FSMI_FLOAT transform[4][4] =
		///   {
		///      { cosAY*cosAZ, cosAZ*sinAX*sinAY - cosAX*sinAZ, cosAX*cosAZ*sinAY + sinAX*sinAZ,  sway  },
		///      { cosAY*sinAZ, cosAX*cosAZ + sinAX*sinAY*sinAZ, -cosAZ*sinAX + cosAX*sinAY*sinAZ, surge },
		///      { -sinAY,      cosAY*sinAX,                     cosAX*cosAY,                      heave },
		///      { 0,           0,                               0,                                1     }
		///   };
		///
		/// 3D transformation matrix, 1st row
		/// NOTE: If this field is provided, set FSMI_POS_BIT_MATRIX in the mask.
		/// </summary>
		public float m11;

		/// <summary>
		/// 3D transformation matrix, 1st row
		/// NOTE: If this field is provided, set FSMI_POS_BIT_MATRIX in the mask.
		/// </summary>
		public float m12;

		/// <summary>
		/// 3D transformation matrix, 1st row
		/// NOTE: If this field is provided, set FSMI_POS_BIT_MATRIX in the mask.
		/// </summary>
		public float m13;

		/// <summary>
		/// 3D transformation matrix, 1st row
		/// NOTE: If this field is provided, set FSMI_POS_BIT_MATRIX in the mask.
		/// </summary>
		public float m14;

		/// <summary>
		/// 3D transformation matrix, 2nd row
		/// NOTE: If this field is provided, set FSMI_POS_BIT_MATRIX in the mask.
		/// </summary>
		public float m21;

		/// <summary>
		/// 3D transformation matrix, 2nd row
		/// NOTE: If this field is provided, set FSMI_POS_BIT_MATRIX in the mask.
		/// </summary>
		public float m22;

		/// <summary>
		/// 3D transformation matrix, 2nd row
		/// NOTE: If this field is provided, set FSMI_POS_BIT_MATRIX in the mask.
		/// </summary>
		public float m23;

		/// <summary>
		/// 3D transformation matrix, 2nd row
		/// NOTE: If this field is provided, set FSMI_POS_BIT_MATRIX in the mask.
		/// </summary>
		public float m24;

		/// <summary>
		/// 3D transformation matrix, 3rd row
		/// NOTE: If this field is provided, set FSMI_POS_BIT_MATRIX in the mask.
		/// </summary>
		public float m31;

		/// <summary>
		/// 3D transformation matrix, 3rd row
		/// NOTE: If this field is provided, set FSMI_POS_BIT_MATRIX in the mask.
		/// </summary>
		public float m32;

		/// <summary>
		/// 3D transformation matrix, 3rd row
		/// NOTE: If this field is provided, set FSMI_POS_BIT_MATRIX in the mask.
		/// </summary>
		public float m33;

		/// <summary>
		/// 3D transformation matrix, 3rd row
		/// NOTE: If this field is provided, set FSMI_POS_BIT_MATRIX in the mask.
		/// </summary>
		public float m34;

		/// <summary>
		/// 3D transformation matrix, 4rd row
		/// NOTE: If this field is provided, set FSMI_POS_BIT_MATRIX in the mask.
		/// </summary>
		public float m41;

		/// <summary>
		/// 3D transformation matrix, 4rd row
		/// NOTE: If this field is provided, set FSMI_POS_BIT_MATRIX in the mask.
		/// </summary>
		public float m42;

		/// <summary>
		/// 3D transformation matrix, 4rd row
		/// NOTE: If this field is provided, set FSMI_POS_BIT_MATRIX in the mask.
		/// </summary>
		public float m43;

		/// <summary>
		/// 3D transformation matrix, 4rd row
		/// NOTE: If this field is provided, set FSMI_POS_BIT_MATRIX in the mask.
		/// </summary>
		public float m44;

		/// <summary>
		/// 0 to 65535, actual speed is not always equal to max speed due to ramps.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.MAX_SPEED in the mask.
		/// </summary>
		public ushort maxSpeed;

		/// <summary>
		/// State of buttons, gun triggers, etc. It is passed directly to the motion platform.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.TRIGGERS in the mask.
		/// </summary>
		public byte triggers;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux1;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux2;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux3;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux4;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux5;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux6;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux7;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux8;
	}

	/// <summary>
	/// This structure defines position of top frame (table) in logical units (percents).
	/// It does not use Inverse Kinematics module so rotation and movements are not always linear.
	///
	/// DEPRECATED: It is recommended to use FSMI_TopTablePositionPhysical.
	/// </summary>
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct FSMI_TopTablePositionLogical
	{
		/// <summary>
		/// Put here sizeof(FSMI_TopTablePositionLogical).
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
		/// NOTE: If the field is provided, set FSMI_POS_BIT.STATE in the mask.
		/// </summary>
		public byte state;

		/// <summary>
		/// -32767 max left, +32767 max right
		/// NOTE: If this field is provided, set FSMI_POS_BIT.POSITION in the mask.
		/// </summary>
		public short roll;

		/// <summary>
		// -32767 max rear, +32767 max front
		/// NOTE: If this field is provided, set FSMI_POS_BIT.POSITION in the mask.
		/// </summary>
		public short pitch;

		/// <summary>
		// -32767 max left, +32767 max right
		/// NOTE: If this field is provided, set FSMI_POS_BIT.POSITION in the mask.
		/// </summary>
		public short yaw;

		/// <summary>
		// -32767 max bottom, +32767 max top
		/// NOTE: If this field is provided, set FSMI_POS_BIT.POSITION in the mask.
		/// </summary>
		public short heave;

		/// <summary>
		// -32767 max left, +32767 max right
		/// NOTE: If this field is provided, set FSMI_POS_BIT.POSITION in the mask.
		/// </summary>
		public short sway; 

		/// <summary>
		// -32767 max rear, +32767 max front
		/// NOTE: If this field is provided, set FSMI_POS_BIT.POSITION in the mask.
		/// </summary>
		public short surge;

		/// <summary>
		/// 0 to 65535, actual speed is not always equal to max speed due to ramps.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.MAX_SPEED in the mask.
		/// </summary>
		public ushort maxSpeed;

		/// <summary>
		/// State of buttons, gun triggers, etc. It is passed directly to the motion platform.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.TRIGGERS in the mask.
		/// </summary>
		public byte triggers;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux1;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux2;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux3;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux4;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux5;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux6;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux7;

		/// <summary>
		/// Custom use field that can be used in script in ForceSeatPM.
		/// NOTE: If this field is provided, set FSMI_POS_BIT.AUX in the mask.
		/// </summary>
		public float aux8;
	}

	/// <summary>
	/// Helpers for mask bits
	/// </summary>
	public struct FSMI_POS_BIT
	{
		public const int STATE     = (1 << 1);
		public const int POSITION  = (1 << 2);
		public const int MATRIX    = (1 << 2);
		public const int MAX_SPEED = (1 << 3);
		public const int TRIGGERS  = (1 << 4);
		public const int AUX       = (1 << 5);
	}
}
