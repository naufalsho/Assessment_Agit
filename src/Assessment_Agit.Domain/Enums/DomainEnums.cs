namespace Assessment_Agit.Domain.Enums;

public enum RequestStatus
{
    PendingManager = 1,
    PendingSystemOwner = 2,
    Approved = 3,
    Rejected = 4
}

public enum AccessLevel
{
    Read = 1,
    Admin = 2
}

public enum EnvironmentType
{
    NonProduction = 1,
    Production = 2
}

