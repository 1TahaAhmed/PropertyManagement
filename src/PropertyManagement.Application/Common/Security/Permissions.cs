namespace PropertyManagement.Application.Common.Security;

public static class Permissions
{
    public static class Properties
    {
        public const string Read = "Permissions.Properties.Read";
        public const string Create = "Permissions.Properties.Create";
        public const string Update = "Permissions.Properties.Update";
        public const string Archive = "Permissions.Properties.Archive";
    }

    public static class Units
    {
        public const string Read = "Permissions.Units.Read";
        public const string Create = "Permissions.Units.Create";
        public const string Update = "Permissions.Units.Update";
        public const string ChangeStatus = "Permissions.Units.ChangeStatus";
    }

    public static class Tenants
    {
        public const string Read = "Permissions.Tenants.Read";
        public const string Create = "Permissions.Tenants.Create";
        public const string Update = "Permissions.Tenants.Update";
        public const string Deactivate = "Permissions.Tenants.Deactivate";
        public const string Blacklist = "Permissions.Tenants.Blacklist";
    }
}
