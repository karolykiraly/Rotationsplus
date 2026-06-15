namespace RotationsPlus.Contracts.Marketplace;

/// <summary>
/// Clinical-rotation program delivery type. Clean replacement for the legacy <c>program_type</c>
/// enumeration (inperson, inperson_research, consultation, consultation_sub, telerotation,
/// teleresearch, dental).
/// </summary>
public enum ProgramType
{
    InPerson,
    InPersonResearch,
    Consultation,
    ConsultationSub,
    TeleRotation,
    TeleResearch,
    Dental
}
