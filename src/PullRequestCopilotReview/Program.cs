using PullRequestCopilotReview;
using PullRequestCopilotReview.Interfaces;
using PullRequestCopilotReview.Services;

ILogger logger = new TextLogger();
IConfigurationHelper configuration;
try
{
    configuration = new ConfigurationHelper();
}
catch (ControlledFailureException ex)
{
    logger.Error(ex.Message);
    Environment.ExitCode = ex.ExitCode;
    return;
}

ICommandRunner commandRunner = new CommandRunner(
    logger,
    line => logger.Info($"[stdout] {line}"),
    line => logger.Info($"[stderr] {line}"));
IDiffParser diffParser = new DiffParser();
IGitHubContextReader contextReader = new GitHubContextReader(configuration);
IGitHubPullRequestService pullRequestService = new GitHubPullRequestService(commandRunner, configuration, diffParser, logger);
IPromptBuilder promptBuilder = new PromptBuilder(configuration);
IReviewResponseValidator reviewResponseValidator = new ReviewResponseValidator();
ICopilotSessionEventLogger eventLogger = new CopilotSessionEventLogger(logger);
ICopilotRunner copilotRunner = new CopilotSdkRunner(new GitHubCopilotSdkClient(), configuration, reviewResponseValidator, eventLogger);
IStepSummaryWriter summaryWriter = new StepSummaryWriter(configuration);
IReviewPublisher reviewPublisher = new ReviewPublisher(commandRunner, configuration);

IReviewApplication app = new PullRequestReviewApp(
    logger,
    configuration,
    contextReader,
    pullRequestService,
    promptBuilder,
    copilotRunner,
    summaryWriter,
    reviewPublisher,
    new PathFilter(),
    new GitHubSecretMasker(),
    new ReviewFilter());

Environment.ExitCode = await app.RunAsync();
