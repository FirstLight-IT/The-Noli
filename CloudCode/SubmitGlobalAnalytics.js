const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY = "account_profile";
const SUBMISSION_KEY = "global_analytics_submission";
const SUBMISSION_STATUS_KEY = "global_analytics_submission_status";
const EXPECTED_SCHEMA_VERSION = 1;
const MAX_CHAPTERS = 20;

module.exports = async ({ params, context, logger }) => {
  const incoming = parseSubmission(params.submissionJson);
  validateSubmission(incoming, context.playerId);

  const cloudSave = new DataApi(context);
  const profile = await loadProtectedValue(
    cloudSave,
    context.projectId,
    context.playerId,
    PROFILE_KEY
  );

  if (!profile) {
    throw new Error("A complete The Noli account is required.");
  }

  if (profile.role === "Librarian") {
    throw new Error("Librarian accounts cannot submit Global Analytics.");
  }

  const existing = await loadProtectedValue(
    cloudSave,
    context.projectId,
    context.playerId,
    SUBMISSION_KEY
  );

  let submission;
  let newlyAcceptedChapters = 0;

  if (existing) {
    if (existing.playthroughId !== incoming.playthroughId) {
      throw new Error(
        "This account has already selected a different official playthrough."
      );
    }

    submission = existing;
    submission.chapters = Array.isArray(submission.chapters)
      ? submission.chapters
      : [];
    const acceptedChapterIds = new Set(
      submission.chapters.map((chapter) => chapter.chapterId)
    );

    for (const chapter of incoming.chapters) {
      if (!acceptedChapterIds.has(chapter.chapterId)) {
        submission.chapters.push(chapter);
        acceptedChapterIds.add(chapter.chapterId);
        newlyAcceptedChapters++;
      }
    }

    submission.lastUpdatedAtUtc = new Date().toISOString();
  } else {
    const now = new Date().toISOString();
    submission = {
      schemaVersion: EXPECTED_SCHEMA_VERSION,
      accountId: context.playerId,
      playthroughId: incoming.playthroughId,
      gameVersion: incoming.gameVersion,
      playthroughCreatedAtUtc: incoming.playthroughCreatedAtUtc,
      acceptedAtUtc: now,
      lastUpdatedAtUtc: now,
      chapters: incoming.chapters
    };
    newlyAcceptedChapters = incoming.chapters.length;
  }

  submission.chapters.sort((left, right) =>
    left.chapterId.localeCompare(right.chapterId)
  );

  await cloudSave.setProtectedItem(context.projectId, context.playerId, {
    key: SUBMISSION_KEY,
    value: submission
  });
  await cloudSave.setProtectedItem(context.projectId, context.playerId, {
    key: SUBMISSION_STATUS_KEY,
    value: "Accepted"
  });

  logger.info("Accepted The Noli Global Analytics submission.", {
    playerId: context.playerId,
    playthroughId: submission.playthroughId,
    newlyAcceptedChapters,
    totalAcceptedChapters: submission.chapters.length
  });

  return {
    status: newlyAcceptedChapters > 0 ? "Accepted" : "AlreadyAccepted",
    newlyAcceptedChapters,
    totalAcceptedChapters: submission.chapters.length,
    officialPlaythroughId: submission.playthroughId
  };
};

async function loadProtectedValue(cloudSave, projectId, playerId, key) {
  const response = await cloudSave.getProtectedItems(projectId, playerId, [key]);
  return response.data.results.length > 0
    ? response.data.results[0].value
    : null;
}

function parseSubmission(value) {
  if (typeof value !== "string" || !value.trim()) {
    throw new Error("A Global Analytics submission is required.");
  }

  try {
    return JSON.parse(value);
  } catch (_) {
    throw new Error("The Global Analytics submission could not be read.");
  }
}

function validateSubmission(submission, authenticatedPlayerId) {
  if (!submission || submission.schemaVersion !== EXPECTED_SCHEMA_VERSION) {
    throw new Error("Unsupported Global Analytics submission version.");
  }

  if (clean(submission.accountId) !== authenticatedPlayerId) {
    throw new Error("The submission does not belong to the signed-in account.");
  }

  if (!isSafeId(submission.playthroughId)) {
    throw new Error("A valid permanent playthrough ID is required.");
  }

  if (!Array.isArray(submission.chapters) ||
      submission.chapters.length < 1 ||
      submission.chapters.length > MAX_CHAPTERS) {
    throw new Error("The submission must contain completed chapter results.");
  }

  const chapterIds = new Set();
  for (const chapter of submission.chapters) {
    validateChapter(chapter);
    if (chapterIds.has(chapter.chapterId)) {
      throw new Error("A chapter result was submitted more than once.");
    }
    chapterIds.add(chapter.chapterId);
  }
}

function validateChapter(chapter) {
  if (!chapter || !isSafeId(chapter.chapterId)) {
    throw new Error("Every result requires a valid chapter ID.");
  }

  if (!Number.isInteger(chapter.quizScore) ||
      !Number.isInteger(chapter.quizMaxScore) ||
      chapter.quizMaxScore <= 0 ||
      chapter.quizScore < 0 ||
      chapter.quizScore > chapter.quizMaxScore) {
    throw new Error(`Chapter '${chapter.chapterId}' has an invalid quiz score.`);
  }

  if (chapter.hasEngagementScore !== true) {
    throw new Error(`Chapter '${chapter.chapterId}' has no finalized engagement score.`);
  }

  requireRate(chapter.quizScoreRatePercent, "quiz score", chapter.chapterId);
  requireRate(chapter.engagementRatePercent, "engagement", chapter.chapterId);
  requireRate(chapter.dialogueSkipRatePercent, "dialogue skip", chapter.chapterId);
  requireRate(chapter.artifactDiscoveryRatePercent, "artifact discovery", chapter.chapterId);

  if (!Number.isFinite(chapter.playTimeSeconds) || chapter.playTimeSeconds < 0) {
    throw new Error(`Chapter '${chapter.chapterId}' has invalid playtime.`);
  }
}

function requireRate(value, label, chapterId) {
  if (!Number.isFinite(value) || value < 0 || value > 100) {
    throw new Error(`Chapter '${chapterId}' has an invalid ${label} rate.`);
  }
}

function isSafeId(value) {
  return /^[A-Za-z0-9_-]{1,64}$/.test(clean(value));
}

function clean(value) {
  return typeof value === "string" ? value.trim() : "";
}
