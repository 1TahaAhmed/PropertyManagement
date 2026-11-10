namespace PropertyManagement.Application.Common.Security;

public static partial class Permissions
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

    public static class Residents
    {
        public const string Read = "Permissions.Residents.Read";
        public const string Create = "Permissions.Residents.Create";
        public const string Update = "Permissions.Residents.Update";
        public const string Archive = "Permissions.Residents.Archive";
        public const string ManageContacts = "Permissions.Residents.ManageContacts";
    }

    public static class Leases
    {
        public const string Read = "Permissions.Leases.Read";
        public const string Create = "Permissions.Leases.Create";
        public const string Update = "Permissions.Leases.Update";
        public const string Submit = "Permissions.Leases.Submit";
        public const string Approve = "Permissions.Leases.Approve";
        public const string Terminate = "Permissions.Leases.Terminate";
        public const string Cancel = "Permissions.Leases.Cancel";
        public const string Renew = "Permissions.Leases.Renew";
    }
}
