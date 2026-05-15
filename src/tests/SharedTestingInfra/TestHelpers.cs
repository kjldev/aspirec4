namespace Aspire.Hosting;

public static class TestHelpers
{
	public static IDistributedApplicationBuilder CreateAppBuilder(string[]? args = null) =>
		DistributedApplication.CreateBuilder(args ?? []);
}
