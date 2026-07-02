namespace RotationsPlus.Contracts.Students;

/// <summary>
/// Whether a licensing exam has been taken — the profile Education tab's "Have you taken …?" answer for
/// USMLE Step 1/2CK/3 and COMLEX Level 2CE/3 (legacy numeric 1/2/3). <see cref="Taken"/> reveals a
/// score + attempts; <see cref="WillTake"/> reveals a scheduled date; <see cref="NoPlan"/> collects
/// nothing further. (COMLEX Level 1 is a simpler yes/no + passed flag, modelled separately.)
/// </summary>
public enum ExamStatus
{
    Taken,
    WillTake,
    NoPlan
}
