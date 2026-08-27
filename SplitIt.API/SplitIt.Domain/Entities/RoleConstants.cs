namespace SplitIt.Domain.Entities
{
    public static class RoleConstants
    {
        public const int SuperAdmin = 1;
        public const int Admin = 2;
        public const int User = 3;

        public static string GetName(int roleId) => roleId switch
        {
            SuperAdmin => "SuperAdmin",
            Admin => "Admin",
            User => "User",
            _ => "User"
        };
    }
}
