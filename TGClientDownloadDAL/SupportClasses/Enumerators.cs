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
}
