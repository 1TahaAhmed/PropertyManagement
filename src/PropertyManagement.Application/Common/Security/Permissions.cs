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

    public static class Residents
    {
        public const string Read = "Permissions.Residents.Read";
        public const string Create = "Permissions.Residents.Create";
        public const string Update = "Permissions.Residents.Update";
        public const string Deactivate = "Permissions.Residents.Deactivate";
        public const string Blacklist = "Permissions.Residents.Blacklist";
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

    public static class Billing
    {
        public const string Preview = "Permissions.Billing.Preview";
        public const string Generate = "Permissions.Billing.Generate";
    }

    public static class Invoices
    {
        public const string Read = "Permissions.Invoices.Read";
        public const string Create = "Permissions.Invoices.Create";
        public const string Issue = "Permissions.Invoices.Issue";
        public const string Void = "Permissions.Invoices.Void";
        public const string Adjust = "Permissions.Invoices.Adjust";
        public const string MarkOverdue = "Permissions.Invoices.MarkOverdue";
    }

    public static class Charges
    {
        public const string Read = "Permissions.Charges.Read";
        public const string Create = "Permissions.Charges.Create";
        public const string Void = "Permissions.Charges.Void";
    }

    public static class Organizations
    {
        public const string Read = "Permissions.Organizations.Read";
        public const string Update = "Permissions.Organizations.Update";
        public const string ManageMembers = "Permissions.Organizations.ManageMembers";
    }

    public static class Invitations
    {
        public const string Read = "Permissions.Invitations.Read";
        public const string Create = "Permissions.Invitations.Create";
        public const string Revoke = "Permissions.Invitations.Revoke";
    }

    public static class Payments
    {
        public const string Read = "Permissions.Payments.Read";
        public const string Create = "Permissions.Payments.Create";
        public const string Confirm = "Permissions.Payments.Confirm";
        public const string Cancel = "Permissions.Payments.Cancel";
        public const string Allocate = "Permissions.Payments.Allocate";
        public const string ReverseAllocation = "Permissions.Payments.ReverseAllocation";
        public const string Refund = "Permissions.Payments.Refund";
        public const string Reconcile = "Permissions.Payments.Reconcile";
    }

    public static class Ledger
    {
        public const string Read = "Permissions.Ledger.Read";
    }

    public static class Audit
    {
        public const string Read = "Permissions.Audit.Read";
    }
}
