namespace slskd.Tests.Unit.Shares;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using slskd;
using slskd.Common.Moderation;
using slskd.Files;
using slskd.Shares;
using Soulseek;
using Xunit;

public class ShareServiceLifecycleTests
{
    [Fact]
    public void ScannerUpdates_DoNotRegressScanProgressOrFiles_WhileScanIsActive()
    {
        var factory = new TestShareRepositoryFactory();
        var service = CreateService(factory, out var scanner, out _);

        scanner.Emit(new SharedFileCacheState
        {
            Filling = true,
            FillProgress = 0.6,
            Files = 120,
        });

        scanner.Emit(new SharedFileCacheState
        {
            Filling = true,
            FillProgress = 0.4,
            Files = 100,
        });

        var current = service.StateMonitor.CurrentValue;

        Assert.Equal(0.6, current.ScanProgress);
        Assert.Equal(120, current.Files);
    }

    [Fact]
    public void TryRemoveHost_DisposesRemovedRepository()
    {
        var factory = new TestShareRepositoryFactory();
        var service = CreateService(factory, out _, out _);

        service.AddOrUpdateHost(new Host("remote"));

        var remoteRepository = factory.Repositories["remote"];
        Assert.False(remoteRepository.Disposed);

        var removed = service.TryRemoveHost("remote");

        Assert.True(removed);
        Assert.True(remoteRepository.Disposed);
    }

    [Fact]
    public void Dispose_UnsubscribesMonitors_AndDisposesOwnedRepositories()
    {
        var factory = new TestShareRepositoryFactory();
        var service = CreateService(factory, out var scanner, out var optionsMonitor);
        service.AddOrUpdateHost(new Host("remote"));

        Assert.Equal(1, scanner.ListenerCount);
        Assert.Equal(1, optionsMonitor.ListenerCount);

        service.Dispose();

        Assert.Equal(0, scanner.ListenerCount);
        Assert.Equal(0, optionsMonitor.ListenerCount);
        Assert.True(factory.Repositories["local"].Disposed);
        Assert.True(factory.Repositories["remote"].Disposed);
    }

    private static ShareService CreateService(TestShareRepositoryFactory factory, out TestShareScanner scanner, out TestOptionsMonitor<Options> optionsMonitor)
    {
        optionsMonitor = new TestOptionsMonitor<Options>(new Options());
        scanner = new TestShareScanner();

        return new ShareService(
            new FileService(optionsMonitor),
            factory,
            optionsMonitor,
            Mock.Of<IModerationProvider>(),
            scanner);
    }

    private sealed class TestShareRepositoryFactory : IShareRepositoryFactory
    {
        public Dictionary<string, TestShareRepository> Repositories { get; } = new(StringComparer.OrdinalIgnoreCase);

        public IShareRepository CreateFromHost(string hostName)
        {
            var repository = new TestShareRepository(hostName);
            Repositories[hostName] = repository;
            return repository;
        }

        public IShareRepository CreateFromHostBackup(string hostName)
        {
            return CreateFromHost($"{hostName}-backup");
        }

        public IShareRepository CreateFromFile(string filename, bool pooling = false)
        {
            return CreateFromHost(filename);
        }
    }

    private sealed class TestShareRepository : IShareRepository
    {
        public TestShareRepository(string name)
        {
            ConnectionString = name;
        }

        public string ConnectionString { get; }
        public bool Disposed { get; private set; }

        public void BackupTo(IShareRepository repository) { }
        public int CountAdvertisableItems() => 0;
        public int CountDirectories(string? parentDirectory = null) => 0;
        public int CountFiles(string? parentDirectory = null) => 0;
        public void Create(bool discardExisting = false) { }
        public void Dispose() => Disposed = true;
        public void DumpTo(string filename) { }
        public void EnableKeepalive(bool enable) { }
        public (string Filename, long Size) FindFileInfo(string maskedFilename) => (string.Empty, 0);
        public Scan? FindLatestScan() => null;
        public (string Domain, string WorkId, string MaskedFilename, bool IsAdvertisable, string ModerationReason, long CheckedAt)? FindContentItem(string contentId) => null;
        public void FlagLatestScanAsSuspect() { }
        public void InsertDirectory(string name, long timestamp) { }
        public void InsertFile(string maskedFilename, string originalFilename, DateTime touchedAt, Soulseek.File file, long timestamp, bool isBlocked = false, bool isQuarantined = false, string? moderationReason = null) { }
        public void InsertScan(long timestamp, Options.SharesOptions options) { }
        public IEnumerable<string> ListDirectories(string? parentDirectory = null) => Array.Empty<string>();
        public IEnumerable<(string ContentId, string Domain, string WorkId, bool IsAdvertisable, string ModerationReason)> ListContentItemsForFile(string maskedFilename)
            => Array.Empty<(string ContentId, string Domain, string WorkId, bool IsAdvertisable, string ModerationReason)>();
        public IEnumerable<Soulseek.File> ListFiles(string? parentDirectory = null, bool includeFullPath = false) => Array.Empty<Soulseek.File>();
        public IEnumerable<(string LocalPath, long Size)> ListLocalPathsAndSizes(string? parentDirectory = null) => Array.Empty<(string LocalPath, long Size)>();
        public IEnumerable<Scan> ListScans(long startedAtOrAfter = 0) => Array.Empty<Scan>();
        public long PruneDirectories(long olderThanTimestamp) => 0;
        public long PruneFiles(long olderThanTimestamp) => 0;
        public void RebuildFilenameIndex() { }
        public void RestoreFrom(IShareRepository repository) { }
        public IEnumerable<Soulseek.File> Search(SearchQuery query, int? limit = null) => Array.Empty<Soulseek.File>();
        public bool TryValidate() => true;
        public bool TryValidate(out IEnumerable<string> problems)
        {
            problems = Array.Empty<string>();
            return true;
        }

        public void UpdateScan(long timestamp, long end) { }
        public void UpsertContentItem(string contentId, string domain, string workId, string maskedFilename, bool isAdvertisable, string? moderationReason, long checkedAt) { }
        public void Vacuum() { }
    }

    private sealed class TestShareScanner : IShareScanner
    {
        private readonly ManagedState<SharedFileCacheState> _state = new();

        public int ListenerCount => _listenerCount;

        private int _listenerCount;

        public IStateMonitor<SharedFileCacheState> StateMonitor => new TestStateMonitor(_state, this);

        public Task ScanAsync(IEnumerable<Share> shares, Options.SharesOptions options, IShareRepository repository) => Task.CompletedTask;

        public bool TryCancelScan() => false;

        public void Emit(SharedFileCacheState state)
        {
            _state.SetValue(_ => state);
        }

        private sealed class TestStateMonitor : IStateMonitor<SharedFileCacheState>
        {
            private readonly ManagedState<SharedFileCacheState> _state;
            private readonly TestShareScanner _owner;

            public TestStateMonitor(ManagedState<SharedFileCacheState> state, TestShareScanner owner)
            {
                _state = state;
                _owner = owner;
            }

            public SharedFileCacheState CurrentValue => _state.CurrentValue;

            public IDisposable OnChange(Action<(SharedFileCacheState Previous, SharedFileCacheState Current)> listener)
            {
                Interlocked.Increment(ref _owner._listenerCount);
                var registration = _state.OnChange(listener);
                return new CallbackDisposable(() =>
                {
                    registration.Dispose();
                    Interlocked.Decrement(ref _owner._listenerCount);
                });
            }
        }

        private sealed class CallbackDisposable : IDisposable
        {
            private readonly Action _dispose;
            private int _disposed;

            public CallbackDisposable(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    _dispose();
                }
            }
        }
    }
}
