namespace PropertyManagement.Application.Common.Results
{
    public interface IResult<TSelf> where TSelf : IResult<TSelf>
    {
        static abstract TSelf Failure(params Error[] errors);
    }
}
