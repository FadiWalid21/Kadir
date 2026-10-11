namespace Kadr.Platform.Domain.Attendance;

public enum AttendanceDayStatus
{
    Present = 0,
    Late = 1,
    Absent = 2,
    Incomplete = 3,
    Weekend = 4,
    Holiday = 5,
    NoShift = 6,
    /// <summary>Reserved for leave module (F17). Never produced by 16a engine.</summary>
    Leave = 7,
}
