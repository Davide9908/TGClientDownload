namespace TGClientDownloadDAL.SupportClasses
{
    public enum DownloadStatus
    {
        SysReserved = 0,
        Downloading,
        Error,
        Success,
        Aborted
    }

    public enum DownloadErrorType
    {
        SysReserved = 0,
        NetworkIssue,
        Cancelled,
        Other
    }

    public enum ChannelStatus
    {
        SysReserved = 0,
        ToConfirm,
        Active,
        Obsolete,
        AccessHashToVerify
    }

    public enum ConfigurationParameterType
    {
        SysReserved = 0,
        Int,
        String,
        Bool,
        DateTime,
        Decimal
    }
}
