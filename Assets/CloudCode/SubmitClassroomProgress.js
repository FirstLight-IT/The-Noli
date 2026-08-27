const { DataApi } = require("@unity-services/cloud-save-1.4");

const ROOM_KEY = "room";
const EXPECTED_SCHEMA_VERSION = 2;
const MAX_CHAPTERS = 20;

module.exports = async ({ params, context, logger }) => {
  const roomId = clean(params.roomId);
  const incoming = parseProgress(params.progressJson);
  validateProgress(incoming, roomId, context.playerId);

  const cloudSave = new DataApi(context);
  const room = await loadPrivateCustomValue(
    cloudSave, context.projectId, roomEntityId(roomId), ROOM_KEY
  );
  if (!room || room.status !== "Active") {
    return failed("This classroom is no longer active.");
  }

  const member = Array.isArray(room.members)
    ? room.members.find(candidate =>
        candidate.accountId === context.playerId && candidate.status === "Active")
    : null;
  if (!member) {
    return failed("You are no longer an active member of this classroom.");
  }

  const existing = await loadProtectedValue(
    cloudSave, context.projectId, context.playerId, progressKey(roomId)
  );
  const now = new Date().toISOString();
  let snapshot;
  let newlyUploadedChapters = 0;

  if (existing && existing.schemaVersion === EXPECTED_SCHEMA_VERSION) {
    if (clean(existing.playthroughId) !== clean(incoming.playthroughId)) {
      throw new Error(
        "This classroom already has analytics from a different playthrough."
      );
    }

    snapshot = existing;
    snapshot.chapters = Array.isArray(snapshot.chapters) ? snapshot.chapters : [];
    const uploadedChapterIds = new Set(
      snapshot.chapters.map(chapter => clean(chapter.chapterId))
    );
    for (const chapter of incoming.chapters) {
      if (!uploadedChapterIds.has(chapter.chapterId)) {
        snapshot.chapters.push(sanitizeChapter(chapter));
        uploadedChapterIds.add(chapter.chapterId);
        newlyUploadedChapters++;
      }
    }
    snapshot.lastSyncedAtUtc = now;
  } else {
    snapshot = sanitizeProgress(incoming);
    snapshot.lastSyncedAtUtc = now;
    newlyUploadedChapters = snapshot.chapters.length;
  }

  snapshot.chapters.sort((left, right) =>
    left.chapterId.localeCompare(right.chapterId)
  );
  await cloudSave.setProtectedItem(context.projectId, context.playerId, {
    key: progressKey(roomId),
    value: snapshot
  });

  logger.info("Synchronized completed classroom analytics.", {
    roomId,
    playerId: context.playerId,
    newlyUploadedChapters,
    totalUploadedChapters: snapshot.chapters.length
  });
  return {
    success: true,
    error: "",
    newlyUploadedChapters,
    totalUploadedChapters: snapshot.chapters.length
  };
};

function parseProgress(value) {
  if (typeof value !== "string" || !value.trim()) {
    throw new Error("A classroom analytics submission is required.");
  }
  try {
    return JSON.parse(value);
  } catch (_) {
    throw new Error("The classroom analytics submission could not be read.");
  }
}

function validateProgress(progress, roomId, playerId) {
  if (!progress || progress.schemaVersion !== EXPECTED_SCHEMA_VERSION) {
    throw new Error("Unsupported classroom analytics version.");
  }
  if (!isSafeId(roomId) || clean(progress.roomId) !== roomId) {
    throw new Error("The classroom analytics has an invalid room ID.");
  }
  if (clean(progress.accountId) !== playerId) {
    throw new Error("The classroom analytics does not belong to this account.");
  }
  if (!isSafeId(progress.playthroughId)) {
    throw new Error("The classroom analytics has an invalid playthrough ID.");
  }
  if (!Array.isArray(progress.chapters) || progress.chapters.length < 1 ||
      progress.chapters.length > MAX_CHAPTERS) {
    throw new Error("Completed classroom chapter analytics are required.");
  }

  const chapterIds = new Set();
  for (const chapter of progress.chapters) {
    if (!chapter || !isSafeId(chapter.chapterId) ||
        chapterIds.has(chapter.chapterId)) {
      throw new Error("The classroom analytics contains an invalid chapter.");
    }
    chapterIds.add(chapter.chapterId);
    if (chapter.hasEngagementScore !== true) {
      throw new Error(`Chapter '${chapter.chapterId}' is not finalized.`);
    }
    requireNonNegativeInteger(chapter.quizScore, "quiz score");
    requirePositiveInteger(chapter.quizMaxScore, "maximum quiz score");
    if (chapter.quizScore > chapter.quizMaxScore) {
      throw new Error("The classroom analytics contains an inconsistent quiz score.");
    }
    requireRate(chapter.quizScoreRatePercent, "quiz score");
    requireRate(chapter.engagementRatePercent, "engagement");
    requireRate(chapter.dialogueSkipRatePercent, "dialogue skip");
    requireRate(chapter.artifactDiscoveryRatePercent, "artifact discovery");
    if (!Number.isFinite(chapter.playTimeSeconds) || chapter.playTimeSeconds < 0) {
      throw new Error("The classroom analytics has invalid playtime.");
    }
  }
}

function sanitizeProgress(progress) {
  return {
    schemaVersion: EXPECTED_SCHEMA_VERSION,
    roomId: clean(progress.roomId),
    accountId: clean(progress.accountId),
    playthroughId: clean(progress.playthroughId),
    lastSyncedAtUtc: "",
    chapters: progress.chapters.map(sanitizeChapter)
  };
}

function sanitizeChapter(chapter) {
  return {
    chapterId: clean(chapter.chapterId),
    recordedAtUtc: clean(chapter.recordedAtUtc),
    quizScore: safeInteger(chapter.quizScore),
    quizMaxScore: safeInteger(chapter.quizMaxScore),
    quizScoreRatePercent: safeRate(chapter.quizScoreRatePercent),
    hasEngagementScore: true,
    engagementRatePercent: safeRate(chapter.engagementRatePercent),
    dialogueSkipRatePercent: safeRate(chapter.dialogueSkipRatePercent),
    artifactDiscoveryRatePercent: safeRate(chapter.artifactDiscoveryRatePercent),
    playTimeSeconds: Math.max(0, Number(chapter.playTimeSeconds) || 0)
  };
}

function requireRate(value, label) {
  if (!Number.isFinite(value) || value < 0 || value > 100) {
    throw new Error(`The classroom analytics has an invalid ${label} rate.`);
  }
}

function requireNonNegativeInteger(value, label) {
  if (!Number.isInteger(value) || value < 0) {
    throw new Error(`The classroom analytics has an invalid ${label}.`);
  }
}

function requirePositiveInteger(value, label) {
  if (!Number.isInteger(value) || value <= 0) {
    throw new Error(`The classroom analytics has an invalid ${label}.`);
  }
}

function safeRate(value) { return Math.min(100, Math.max(0, Number(value) || 0)); }
function safeInteger(value) { return Math.max(0, Number.isInteger(value) ? value : 0); }
function isSafeId(value) { return /^[A-Za-z0-9_-]{1,64}$/.test(clean(value)); }
function clean(value) { return typeof value === "string" ? value.trim() : ""; }
function failed(error) { return { success: false, error }; }
function roomEntityId(roomId) { return `classroom_${roomId}`; }
function progressKey(roomId) { return `classroom_progress_${roomId}`; }

async function loadProtectedValue(cloudSave, projectId, playerId, key) {
  const response = await cloudSave.getProtectedItems(projectId, playerId, [key]);
  const item = response.data.results.find(result => result.key === key);
  return item ? item.value : null;
}

async function loadPrivateCustomValue(cloudSave, projectId, customId, key) {
  const response = await cloudSave.getPrivateCustomItems(projectId, customId);
  const item = response.data.results.find(result => result.key === key);
  return item ? item.value : null;
}

module.exports.params = {
  roomId: { type: "String", required: true },
  progressJson: { type: "String", required: true }
};
