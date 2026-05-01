import './DiscoveryInbox.css';
import { getAcquisitionProfile } from '../../lib/acquisitionProfiles';
import {
  createAcquisitionPlansFromDiscoveryInbox,
  executeAcquisitionPlanSearches,
  executeAcquisitionPlanWishlistRequests,
  getAcquisitionPlans,
} from '../../lib/acquisitionPlans';
import {
  bulkUpdateDiscoveryInboxItems,
  getDiscoveryInboxSnoozeStatus,
  getDiscoveryInboxItems,
  snoozeDiscoveryInboxItem,
  updateDiscoveryInboxItemState,
} from '../../lib/discoveryInbox';
import {
  buildDiscoveryInboxReviewSummary,
  classifyDiscoveryInboxImpact,
} from '../../lib/discoveryInboxReview';
import {
  buildWatchlistDiscoverySeed,
  buildWatchlistExpansionSummary,
  buildWatchlistSchedulePreview,
  buildWatchlistSummary,
  getWatchlists,
  recordWatchlistExpansionDecision,
  recordWatchlistManualScan,
  saveWatchlist,
  watchlistAcquisitionProfileOptions,
  watchlistCountryOptions,
  watchlistFormatOptions,
  watchlistKindOptions,
  watchlistReleaseTypeOptions,
  watchlistScheduleOptions,
} from '../../lib/watchlists';
import { addDiscoveryInboxItem } from '../../lib/discoveryInbox';
import { create as createSearch } from '../../lib/searches';
import { create as createWishlist } from '../../lib/wishlist';
import React, { useEffect, useMemo, useState } from 'react';
import {
  Button,
  Dropdown,
  Form,
  Header,
  Icon,
  Input,
  Label,
  Popup,
  Segment,
} from 'semantic-ui-react';

const stateColors = {
  Approved: 'green',
  Downloading: 'blue',
  Failed: 'red',
  Imported: 'teal',
  Rejected: 'red',
  Snoozed: 'grey',
  Staged: 'violet',
  Suggested: 'yellow',
};

const formatTimestamp = (timestamp) => {
  const date = new Date(timestamp);
  return Number.isNaN(date.getTime()) ? 'Unknown time' : date.toLocaleString();
};

const expansionCandidateColor = (status) => {
  if (status === 'Approved') {
    return 'green';
  }

  if (status === 'Rejected') {
    return 'red';
  }

  return 'grey';
};

const expansionCandidateIcon = (status) => {
  if (status === 'Approved') {
    return 'check';
  }

  if (status === 'Rejected') {
    return 'ban';
  }

  return 'user plus';
};

const DiscoveryInbox = () => {
  const [items, setItems] = useState([]);
  const [plans, setPlans] = useState([]);
  const [planExecutionStatus, setPlanExecutionStatus] = useState('');
  const [mobileReviewId, setMobileReviewId] = useState('');
  const [watchlists, setWatchlists] = useState([]);
  const [watchTarget, setWatchTarget] = useState('');
  const [watchKind, setWatchKind] = useState('Artist');
  const [watchReleaseTypes, setWatchReleaseTypes] = useState([
    'Album',
    'EP',
    'Single',
  ]);
  const [watchCountry, setWatchCountry] = useState('Any');
  const [watchFormat, setWatchFormat] = useState('Any');
  const [watchSchedule, setWatchSchedule] = useState('Manual only');
  const [watchCooldownDays, setWatchCooldownDays] = useState(7);
  const [watchAcquisitionProfile, setWatchAcquisitionProfile] =
    useState('lossless-exact');
  const [watchExpansionCandidates, setWatchExpansionCandidates] = useState('');

  const refreshItems = () => {
    setItems(getDiscoveryInboxItems());
    setPlans(getAcquisitionPlans());
    setWatchlists(getWatchlists());
  };

  useEffect(() => {
    refreshItems();
  }, []);

  const suggestedIds = useMemo(
    () => items.filter((item) => item.state === 'Suggested').map((item) => item.id),
    [items],
  );
  const approvedItems = useMemo(
    () => items.filter((item) => item.state === 'Approved'),
    [items],
  );
  const executablePlans = useMemo(
    () => plans.filter((plan) => ['Planned', 'Ready'].includes(plan.state)),
    [plans],
  );
  const wishlistEligiblePlans = useMemo(
    () => executablePlans.filter((plan) => !plan.wishlistRequestId),
    [executablePlans],
  );

  const stateCounts = useMemo(
    () =>
      items.reduce(
        (counts, item) => ({
          ...counts,
          [item.state]: (counts[item.state] || 0) + 1,
        }),
        {},
      ),
    [items],
  );
  const reviewSummary = useMemo(
    () => buildDiscoveryInboxReviewSummary(items),
    [items],
  );
  const mobileReviewIndex = useMemo(() => {
    const foundIndex = items.findIndex((item) => item.id === mobileReviewId);
    return foundIndex >= 0 ? foundIndex : 0;
  }, [items, mobileReviewId]);
  const mobileReviewItem = items[mobileReviewIndex] || null;
  const watchlistSummary = useMemo(
    () => buildWatchlistSummary(watchlists),
    [watchlists],
  );

  const setItemState = (item, state) => {
    setItems(updateDiscoveryInboxItemState(item.id, state));
  };

  const snoozeItem = (item, days = 7) => {
    setItems(snoozeDiscoveryInboxItem(item.id, days));
  };

  const setMobileReviewOffset = (offset) => {
    if (items.length === 0) {
      setMobileReviewId('');
      return;
    }

    const nextIndex = (mobileReviewIndex + offset + items.length) % items.length;
    setMobileReviewId(items[nextIndex].id);
  };

  const bulkSetSuggested = (state) => {
    setItems(bulkUpdateDiscoveryInboxItems(suggestedIds, state));
  };

  const createPlansForApproved = () => {
    const result = createAcquisitionPlansFromDiscoveryInbox(approvedItems);
    const createdIds = new Set(result.createdPlans.map((plan) => plan.sourceId));
    setPlans(result.plans);
    setPlanExecutionStatus(
      result.createdPlans.length > 0
        ? `Created ${result.createdPlans.length} acquisition plan${
            result.createdPlans.length === 1 ? '' : 's'
          }.`
        : 'No new acquisition plans were created.',
    );

    if (createdIds.size > 0) {
      setItems(
        bulkUpdateDiscoveryInboxItems(
          approvedItems
            .filter((item) => createdIds.has(item.id))
            .map((item) => item.id),
          'Staged',
        ),
      );
    }
  };

  const executeReadyPlans = async () => {
    setPlanExecutionStatus('Queueing bounded acquisition searches...');
    const result = await executeAcquisitionPlanSearches(
      executablePlans.map((plan) => plan.id),
      { createSearch },
    );
    setPlans(result.plans);
    setPlanExecutionStatus(
      `Queued ${result.executed} acquisition search job${
        result.executed === 1 ? '' : 's'
      }; ${result.failed} failed; ${result.skipped} skipped by policy.`,
    );
  };

  const createWishlistForReadyPlans = async () => {
    setPlanExecutionStatus('Creating manual Wishlist requests...');
    const result = await executeAcquisitionPlanWishlistRequests(
      wishlistEligiblePlans.map((plan) => plan.id),
      { createWishlist },
    );
    setPlans(result.plans);
    setPlanExecutionStatus(
      `Created ${result.created} Wishlist request${
        result.created === 1 ? '' : 's'
      }; ${result.failed} failed; ${result.skipped} skipped by policy.`,
    );
  };

  const addWatchlist = () => {
    const target = watchTarget.trim();
    if (!target) {
      return;
    }

    setWatchlists(
      saveWatchlist({
        acquisitionProfile: watchAcquisitionProfile,
        cooldownDays: watchCooldownDays,
        country: watchCountry,
        expansionCandidates: watchExpansionCandidates
          .split(',')
          .map((candidate) => candidate.trim())
          .filter(Boolean),
        format: watchFormat,
        kind: watchKind,
        releaseTypes: watchReleaseTypes,
        schedule: watchSchedule,
        target,
      }),
    );
    setWatchTarget('');
    setWatchExpansionCandidates('');
  };

  const previewWatchlistScan = (watchlist) => {
    setWatchlists(recordWatchlistManualScan(watchlist.id));
  };

  const decideWatchlistExpansion = (watchlist, candidate, decision) => {
    setWatchlists(
      recordWatchlistExpansionDecision(watchlist.id, candidate.name, decision),
    );
  };

  const seedWatchlistReview = (watchlist) => {
    addDiscoveryInboxItem(buildWatchlistDiscoverySeed(watchlist));
    refreshItems();
  };

  return (
    <Segment
      className="discovery-inbox"
      raised
    >
      <div className="discovery-inbox-header">
        <Header as="h2">
          <Icon name="inbox" />
          <Header.Content>
            Acquisition Review
            <Header.Subheader>
              Review passive, imported, and generated acquisition candidates.
              Manual Search stays direct.
            </Header.Subheader>
          </Header.Content>
        </Header>
        <div className="discovery-inbox-actions">
          <Popup
            content="Approve every currently suggested item. This marks the candidates ready for the next acquisition step, but this screen does not start downloads."
            position="top center"
            trigger={
              <Button
                aria-label="Approve suggested discovery items"
                disabled={suggestedIds.length === 0}
                onClick={() => bulkSetSuggested('Approved')}
                positive
              >
                <Icon name="check" />
                Approve Suggested
              </Button>
            }
          />
          <Popup
            content="Reject every currently suggested item so the same evidence does not stay in the review queue."
            position="top center"
            trigger={
              <Button
                aria-label="Reject suggested discovery items"
                disabled={suggestedIds.length === 0}
                negative
                onClick={() => bulkSetSuggested('Rejected')}
              >
                <Icon name="ban" />
                Reject Suggested
              </Button>
            }
          />
          <Popup
            content="Create review-only acquisition plans for approved items. Plans show provider order and manual execution policy, but do not start searches or downloads."
            position="top center"
            trigger={
              <Button
                aria-label="Create acquisition plans for approved discovery items"
                disabled={approvedItems.length === 0}
                onClick={createPlansForApproved}
                primary
              >
                <Icon name="tasks" />
                Plan Approved
              </Button>
            }
          />
        </div>
      </div>

      <div className="discovery-inbox-summary">
        {['Suggested', 'Approved', 'Staged', 'Snoozed', 'Rejected'].map((state) => (
          <Label
            color={stateColors[state]}
            key={state}
          >
            {state}
            <Label.Detail>{stateCounts[state] || 0}</Label.Detail>
          </Label>
        ))}
        <Label color="blue">
          Plans
          <Label.Detail>{plans.length}</Label.Detail>
        </Label>
      </div>

      <Segment className="discovery-inbox-impact-summary">
        <Header as="h3">
          <Icon name="dashboard" />
          Review impact
          <Header.Subheader>
            Batch readiness from visible candidate evidence. This does not start
            network activity.
          </Header.Subheader>
        </Header>
        <div className="discovery-inbox-summary">
          <Label color="green">
            Local/manual
            <Label.Detail>{reviewSummary['local-manual']}</Label.Detail>
          </Label>
          <Label color="orange">
            Provider review
            <Label.Detail>{reviewSummary['provider-review']}</Label.Detail>
          </Label>
          <Label color="red">
            Network risk
            <Label.Detail>{reviewSummary['network-risk']}</Label.Detail>
          </Label>
          <Label color="blue">
            Needs estimate
            <Label.Detail>{reviewSummary['needs-estimate']}</Label.Detail>
          </Label>
          <Label color={reviewSummary.canBulkApproveSafely ? 'green' : 'grey'}>
            Batch approval
            <Label.Detail>
              {reviewSummary.canBulkApproveSafely ? 'clear' : 'review'}
            </Label.Detail>
          </Label>
        </div>
      </Segment>

      {mobileReviewItem && (
        <Segment className="discovery-inbox-mobile-review">
          <div className="discovery-inbox-mobile-review-head">
            <Header as="h3">
              <Icon name="mobile alternate" />
              Mobile Review
              <Header.Subheader>
                One-at-a-time discovery review for narrow screens. Actions only
                update local review state.
              </Header.Subheader>
            </Header>
            <Label color={stateColors[mobileReviewItem.state] || 'grey'}>
              {mobileReviewIndex + 1} / {items.length}
            </Label>
          </div>
          <div className="discovery-inbox-mobile-review-title">
            Reviewing: {mobileReviewItem.title}
          </div>
          <div className="discovery-inbox-meta">
            <Icon name="map signs" />
            {mobileReviewItem.source}
            <span> · </span>
            <Icon name={getAcquisitionProfile(mobileReviewItem.acquisitionProfile).icon} />
            {getAcquisitionProfile(mobileReviewItem.acquisitionProfile).label}
          </div>
          <div className="discovery-inbox-impact">
            {mobileReviewItem.networkImpact}
          </div>
          <div className="discovery-inbox-mobile-review-actions">
            <Popup
              content="Move to the previous discovery candidate without changing review state."
              position="top center"
              trigger={
                <Button
                  aria-label="Review previous discovery item"
                  disabled={items.length < 2}
                  icon="chevron left"
                  onClick={() => setMobileReviewOffset(-1)}
                />
              }
            />
            <Popup
              content="Approve this candidate for a later acquisition plan. No search or download starts here."
              position="top center"
              trigger={
                <Button
                  aria-label={`Review tray approve ${mobileReviewItem.title}`}
                  disabled={mobileReviewItem.state === 'Approved'}
                  onClick={() => setItemState(mobileReviewItem, 'Approved')}
                  positive
                >
                  <Icon name="check" />
                  Approve
                </Button>
              }
            />
            <Popup
              content="Snooze this candidate for seven days while keeping its evidence."
              position="top center"
              trigger={
                <Button
                  aria-label={`Review tray snooze ${mobileReviewItem.title}`}
                  disabled={mobileReviewItem.state === 'Snoozed'}
                  onClick={() => snoozeItem(mobileReviewItem, 7)}
                >
                  <Icon name="clock" />
                  Snooze
                </Button>
              }
            />
            <Popup
              content="Reject this candidate and keep the local decision with the saved evidence."
              position="top center"
              trigger={
                <Button
                  aria-label={`Review tray reject ${mobileReviewItem.title}`}
                  disabled={mobileReviewItem.state === 'Rejected'}
                  negative
                  onClick={() => setItemState(mobileReviewItem, 'Rejected')}
                >
                  <Icon name="ban" />
                  Reject
                </Button>
              }
            />
            <Popup
              content="Move to the next discovery candidate without changing review state."
              position="top center"
              trigger={
                <Button
                  aria-label="Review next discovery item"
                  disabled={items.length < 2}
                  icon="chevron right"
                  onClick={() => setMobileReviewOffset(1)}
                />
              }
            />
          </div>
        </Segment>
      )}

      <Segment className="discovery-inbox-watchlists">
        <Header as="h3">
          <Icon name="rss" />
          Watchlists
          <Header.Subheader>
            Local release-radar targets. Manual scans are previews until
            provider-backed discovery is enabled.
          </Header.Subheader>
        </Header>
        <Form className="discovery-inbox-watchlist-form">
          <Form.Group widths="equal">
            <Form.Field>
              <label>Target</label>
              <Input
                aria-label="Watchlist target"
                onChange={(event) => setWatchTarget(event.target.value)}
                placeholder="Artist, label, playlist, or collection"
                value={watchTarget}
              />
            </Form.Field>
            <Form.Field>
              <label>Type</label>
              <Dropdown
                aria-label="Watchlist type"
                onChange={(_event, data) => setWatchKind(data.value)}
                options={watchlistKindOptions}
                selection
                value={watchKind}
              />
            </Form.Field>
          </Form.Group>
          <Form.Group widths="equal">
            <Form.Field>
              <label>Release types</label>
              <Dropdown
                aria-label="Watchlist release types"
                fluid
                multiple
                onChange={(_event, data) => setWatchReleaseTypes(data.value)}
                options={watchlistReleaseTypeOptions}
                selection
                value={watchReleaseTypes}
              />
            </Form.Field>
            <Form.Field>
              <label>Country</label>
              <Dropdown
                aria-label="Watchlist country"
                onChange={(_event, data) => setWatchCountry(data.value)}
                options={watchlistCountryOptions}
                selection
                value={watchCountry}
              />
            </Form.Field>
            <Form.Field>
              <label>Format</label>
              <Dropdown
                aria-label="Watchlist format"
                onChange={(_event, data) => setWatchFormat(data.value)}
                options={watchlistFormatOptions}
                selection
                value={watchFormat}
              />
            </Form.Field>
          </Form.Group>
          <Form.Group widths="equal">
            <Form.Field>
              <label>Schedule</label>
              <Dropdown
                aria-label="Watchlist schedule"
                onChange={(_event, data) => setWatchSchedule(data.value)}
                options={watchlistScheduleOptions}
                selection
                value={watchSchedule}
              />
            </Form.Field>
            <Form.Field>
              <label>Cooldown days</label>
              <Input
                aria-label="Watchlist cooldown days"
                min={1}
                max={30}
                onChange={(event) => setWatchCooldownDays(event.target.value)}
                type="number"
                value={watchCooldownDays}
              />
            </Form.Field>
            <Form.Field>
              <label>Profile policy</label>
              <Dropdown
                aria-label="Watchlist acquisition profile"
                onChange={(_event, data) => setWatchAcquisitionProfile(data.value)}
                options={watchlistAcquisitionProfileOptions}
                selection
                value={watchAcquisitionProfile}
              />
            </Form.Field>
          </Form.Group>
          <Form.Field>
            <label>Similar artist candidates</label>
            <Input
              aria-label="Watchlist similar artist candidates"
              onChange={(event) => setWatchExpansionCandidates(event.target.value)}
              placeholder="Comma-separated artists for review-only expansion"
              value={watchExpansionCandidates}
            />
          </Form.Field>
          <Popup
            content="Add this target to the browser-local watchlist. This does not contact metadata providers or scan Soulseek."
            position="top center"
            trigger={
              <Button
                aria-label="Add watchlist target"
                disabled={!watchTarget.trim()}
                onClick={addWatchlist}
                primary
                type="button"
              >
                <Icon name="plus" />
                Add Watch
              </Button>
            }
          />
        </Form>
        <div className="discovery-inbox-summary">
          <Label color="blue">
            Total
            <Label.Detail>{watchlistSummary.total}</Label.Detail>
          </Label>
          <Label color="green">
            Artists
            <Label.Detail>{watchlistSummary.Artist}</Label.Detail>
          </Label>
          <Label color="purple">
            Labels
            <Label.Detail>{watchlistSummary.Label}</Label.Detail>
          </Label>
          <Label color="teal">
            Scheduled
            <Label.Detail>{watchlistSummary.scheduled}</Label.Detail>
          </Label>
        </div>
        {watchlists.length > 0 && (
          <div className="discovery-inbox-watchlist-grid">
            {watchlists.map((watchlist) => {
              const expansionSummary = buildWatchlistExpansionSummary(watchlist);
              const schedulePreview = buildWatchlistSchedulePreview(watchlist);

              return (
                <div
                  className="discovery-inbox-watchlist"
                  key={watchlist.id}
                >
                  <div>
                    <strong>{watchlist.target}</strong>
                    <div className="discovery-inbox-meta">
                      <Icon name="tag" />
                      {watchlist.kind}
                      <span> · </span>
                      <Icon name="filter" />
                      {watchlist.releaseTypes.join(', ')}
                      <span> · </span>
                      <Icon name="world" />
                      {watchlist.country}
                      <span> · </span>
                      <Icon name="music" />
                      {watchlist.format}
                      <span> · </span>
                      <Icon name="clock" />
                      {watchlist.schedule}
                    </div>
                    <div className="discovery-inbox-impact-labels">
                      <Label
                        basic
                        color={schedulePreview.enabled ? 'orange' : 'grey'}
                      >
                        <Icon
                          name={schedulePreview.enabled ? 'calendar check' : 'lock'}
                        />
                        {schedulePreview.label}
                      </Label>
                      <Label basic>
                        <Icon name="hourglass half" />
                        Cooldown {schedulePreview.cooldown}
                      </Label>
                      <Label basic>
                        <Icon name="gem outline" />
                        {schedulePreview.profileLabel}
                      </Label>
                      {expansionSummary.total > 0 && (
                        <Label basic>
                          <Icon name="users" />
                          Expansions {expansionSummary.Pending} pending
                        </Label>
                      )}
                    </div>
                    <div className="discovery-inbox-impact">
                      {schedulePreview.networkImpact}
                    </div>
                    {expansionSummary.total > 0 && (
                      <div className="discovery-inbox-impact">
                        Similar-artist expansion is review-only. Approving a
                        candidate creates a manual Artist watchlist and does not
                        search, browse peers, or download.
                      </div>
                    )}
                    {watchlist.expansionCandidates.length > 0 && (
                      <div className="discovery-inbox-impact-labels">
                        {watchlist.expansionCandidates.map((candidate) => (
                          <Label
                            basic
                            color={expansionCandidateColor(candidate.status)}
                            key={candidate.name}
                          >
                            <Icon name={expansionCandidateIcon(candidate.status)} />
                            {candidate.name}
                            <Label.Detail>{candidate.status}</Label.Detail>
                          </Label>
                        ))}
                      </div>
                    )}
                    {watchlist.lastScanPreview && (
                      <div className="discovery-inbox-impact">
                        {watchlist.lastScanPreview}
                      </div>
                    )}
                  </div>
                  <div className="discovery-inbox-watchlist-actions">
                    <Popup
                      content="Record a local manual scan preview for this watchlist. This does not fetch metadata or contact peers."
                      position="top center"
                      trigger={
                        <Button
                          aria-label={`Preview scan ${watchlist.target}`}
                          icon="search"
                          onClick={() => previewWatchlistScan(watchlist)}
                          size="small"
                        />
                      }
                    />
                    {watchlist.expansionCandidates
                      .filter((candidate) => candidate.status === 'Pending')
                      .map((candidate) => (
                        <React.Fragment key={candidate.name}>
                          <Popup
                            content={`Approve ${candidate.name} as a browser-local Artist watchlist expansion. This does not search providers, browse peers, or download.`}
                            position="top center"
                            trigger={
                              <Button
                                aria-label={`Approve similar artist ${candidate.name}`}
                                icon="user plus"
                                onClick={() =>
                                  decideWatchlistExpansion(
                                    watchlist,
                                    candidate,
                                    'Approved',
                                  )
                                }
                                size="small"
                              />
                            }
                          />
                          <Popup
                            content={`Reject ${candidate.name} as a similar-artist expansion so it remains recorded without creating a new watchlist.`}
                            position="top center"
                            trigger={
                              <Button
                                aria-label={`Reject similar artist ${candidate.name}`}
                                icon="user times"
                                negative
                                onClick={() =>
                                  decideWatchlistExpansion(
                                    watchlist,
                                    candidate,
                                    'Rejected',
                                  )
                                }
                                size="small"
                              />
                            }
                          />
                        </React.Fragment>
                      ))}
                    <Popup
                      content="Create a Discovery Inbox review seed from this watchlist target without starting a provider lookup, search, or download."
                      position="top center"
                      trigger={
                        <Button
                          aria-label={`Send ${watchlist.target} to Discovery Inbox`}
                          icon="inbox"
                          onClick={() => seedWatchlistReview(watchlist)}
                          size="small"
                        />
                      }
                    />
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </Segment>

      {plans.length > 0 && (
        <Segment className="discovery-inbox-plans">
          <div className="discovery-inbox-plans-header">
            <Header as="h3">
              <Icon name="tasks" />
              Acquisition Plans
              <Header.Subheader>
                Manual plan queue. Execution starts bounded search jobs only.
              </Header.Subheader>
            </Header>
            <div className="discovery-inbox-plan-actions">
              <Popup
                content="Create Wishlist entries for the first ready acquisition plans with auto-download disabled. This saves follow-up work without starting searches, peer browses, or downloads."
                position="top center"
                trigger={
                  <Button
                    aria-label="Create Wishlist requests for ready acquisition plans"
                    disabled={wishlistEligiblePlans.length === 0}
                    icon
                    onClick={createWishlistForReadyPlans}
                    size="small"
                  >
                    <Icon name="star outline" />
                    Wishlist Ready
                  </Button>
                }
              />
              <Popup
                content="Queue backend searches for the first ready acquisition plans using their selected profiles. This may contact the Soulseek network through normal search, but it does not browse peers or download files."
                position="top center"
                trigger={
                  <Button
                    aria-label="Execute ready acquisition plans"
                    disabled={executablePlans.length === 0}
                    icon
                    onClick={executeReadyPlans}
                    primary
                    size="small"
                  >
                    <Icon name="play" />
                    Execute Ready
                  </Button>
                }
              />
            </div>
          </div>
          {planExecutionStatus && (
            <div className="discovery-inbox-plan-status">
              {planExecutionStatus}
            </div>
          )}
          <div className="discovery-inbox-plan-grid">
            {plans.map((plan) => {
              const profile = getAcquisitionProfile(plan.acquisitionProfile);

              return (
                <div
                  className="discovery-inbox-plan"
                  key={plan.id}
                >
                  <div>
                    <strong>{plan.title}</strong>
                    <div className="discovery-inbox-meta">
                      <Icon name={profile.icon} />
                      {profile.label}
                      <span> · </span>
                      <Icon name="lock" />
                      {plan.manualOnly ? 'Manual execution' : 'Automation allowed'}
                    </div>
                    <div className="discovery-inbox-plan-providers">
                      {plan.providerPriority.join(' → ')}
                    </div>
                    {plan.queuedSearchId && (
                      <div className="discovery-inbox-meta">
                        <Icon name="search" />
                        Search job {plan.queuedSearchId}
                      </div>
                    )}
                    {plan.wishlistRequestId && (
                      <div className="discovery-inbox-meta">
                        <Icon name="star outline" />
                        Wishlist request {plan.wishlistRequestId}
                      </div>
                    )}
                  </div>
                  <Label color="blue">{plan.state}</Label>
                </div>
              );
            })}
          </div>
        </Segment>
      )}

      {items.length === 0 ? (
        <Segment
          className="discovery-inbox-empty"
          placeholder
        >
          <Header icon>
            <Icon name="inbox" />
            No discovery candidates yet
          </Header>
          <p>
            Save a search phrase into the inbox from Search to review it before
            it becomes download work.
          </p>
        </Segment>
      ) : (
        <div className="discovery-inbox-grid">
          {items.map((item) => {
            const profile = getAcquisitionProfile(item.acquisitionProfile);
            const impact = classifyDiscoveryInboxImpact(item);
            const snoozeStatus = getDiscoveryInboxSnoozeStatus(item);

            return (
              <Segment
                className="discovery-inbox-card"
                key={item.id}
              >
                <div className="discovery-inbox-card-head">
                  <div>
                    <div className="discovery-inbox-title">{item.title}</div>
                    <div className="discovery-inbox-meta">
                      <Icon name="clock outline" />
                      {formatTimestamp(item.createdAt)}
                      <span> · </span>
                      <Icon name={profile.icon} />
                      {profile.label}
                      <span> · </span>
                      <Icon name="map signs" />
                      {item.source}
                    </div>
                  </div>
                  <Label color={stateColors[item.state] || 'grey'}>
                    {item.state}
                  </Label>
                </div>
                {snoozeStatus && (
                  <div className="discovery-inbox-snooze">
                    <Label color={snoozeStatus.color}>
                      <Icon name={snoozeStatus.isDue ? 'bell' : 'clock'} />
                      {snoozeStatus.label}
                      {item.snoozedUntil && (
                        <Label.Detail>
                          {formatTimestamp(item.snoozedUntil)}
                        </Label.Detail>
                      )}
                    </Label>
                  </div>
                )}
                <div className="discovery-inbox-reason">
                  <strong>Why:</strong> {item.reason}
                </div>
                <div className="discovery-inbox-impact">
                  <strong>Network impact:</strong> {item.networkImpact}
                </div>
                <div className="discovery-inbox-impact-labels">
                  <Popup
                    content="Impact class is inferred from saved evidence text so reviewers can spot provider or network risk before approving."
                    position="top center"
                    trigger={
                      <Label color={impact.color}>
                        <Icon name={impact.icon} />
                        {impact.label}
                      </Label>
                    }
                  />
                </div>
                <div className="discovery-inbox-card-actions">
                  <Popup
                    content="Load this candidate into the one-at-a-time mobile review tray."
                    position="top center"
                    trigger={
                      <Button
                        aria-label={`Select ${item.title} for mobile review`}
                        onClick={() => setMobileReviewId(item.id)}
                        size="small"
                      >
                        <Icon name="mobile alternate" />
                        Review
                      </Button>
                    }
                  />
                  <Popup
                    content="Mark this candidate approved for a later acquisition step. No download starts from this button."
                    position="top center"
                    trigger={
                      <Button
                        aria-label={`Approve ${item.title}`}
                        disabled={item.state === 'Approved'}
                        onClick={() => setItemState(item, 'Approved')}
                        positive
                        size="small"
                      >
                        <Icon name="check" />
                        Approve
                      </Button>
                    }
                  />
                  <Popup
                    content="Snooze this candidate for 7 days so it stays out of the immediate review set without losing the evidence."
                    position="top center"
                    trigger={
                      <Button
                        aria-label={`Snooze ${item.title}`}
                        disabled={item.state === 'Snoozed'}
                        onClick={() => snoozeItem(item, 7)}
                        size="small"
                      >
                        <Icon name="clock" />
                        7d
                      </Button>
                    }
                  />
                  <Popup
                    content="Return this snoozed candidate to the suggested review queue without changing its evidence."
                    position="top center"
                    trigger={
                      <Button
                        aria-label={`Unsnooze ${item.title}`}
                        disabled={item.state !== 'Snoozed'}
                        onClick={() => setItemState(item, 'Suggested')}
                        size="small"
                      >
                        <Icon name="undo" />
                        Unsnooze
                      </Button>
                    }
                  />
                  <Popup
                    content="Reject this candidate and keep that decision with the saved evidence."
                    position="top center"
                    trigger={
                      <Button
                        aria-label={`Reject ${item.title}`}
                        disabled={item.state === 'Rejected'}
                        negative
                        onClick={() => setItemState(item, 'Rejected')}
                        size="small"
                      >
                        <Icon name="ban" />
                        Reject
                      </Button>
                    }
                  />
                </div>
              </Segment>
            );
          })}
        </div>
      )}
    </Segment>
  );
};

export default DiscoveryInbox;
