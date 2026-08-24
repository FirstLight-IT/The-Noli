const { DataApi } = require("@unity-services/cloud-save-1.4");

// This endpoint has no client parameters.

const PROFILE_KEY = "account_profile";
const SUBMISSION_KEY = "global_analytics_submission";
const SUBMISSION_STATUS_KEY = "global_analytics_submission_status";

module.exports = async ({ context, logger }) => {
  const cloudSave = new DataApi(context);
  const librarianProfile = await loadProtectedValue(
    cloudSave,
    context.projectId,
    context.playerId,
    PROFILE_KEY
  );

  if (!librarianProfile ||
      librarianProfile.role !== "Librarian" ||
      librarianProfile.effectiveRole !== "Librarian") {
    throw new Error("Only a verified Librarian may view Global Analytics.");
  }

  const query = {
    fields: [
      {
        asc: true,
        key: SUBMISSION_STATUS_KEY,
        op: "EQ",
        value: "Accepted"
      }
    ],
    returnKeys: [SUBMISSION_STATUS_KEY],
    limit: 100
  };

  const response = await cloudSave.queryProtectedPlayerData(
    context.projectId,
    query
  );
  const indexedPlayers = response.data.results || [];
  const chapterTotals = new Map();
  let participantCount = 0;

  for (const result of indexedPlayers) {
    const accountId = result.id || result.playerId;
    if (!accountId) {
      continue;
    }

    const submission = await loadProtectedValue(
      cloudSave,
      context.projectId,
      accountId,
      SUBMISSION_KEY
    );

    if (!submission || !Array.isArray(submission.chapters) ||
        submission.chapters.length === 0) {
      continue;
    }

    participantCount++;

    for (const chapter of submission.chapters) {
      if (!chapter || typeof chapter.chapterId !== "string") {
        continue;
      }

      let totals = chapterTotals.get(chapter.chapterId);
      if (!totals) {
        totals = {
          chapterId: chapter.chapterId,
          participantCount: 0,
          engagementTotal: 0,
          quizTotal: 0,
          dialogueSkipTotal: 0,
          artifactDiscoveryTotal: 0,
          playTimeTotal: 0
        };
        chapterTotals.set(chapter.chapterId, totals);
      }

      totals.participantCount++;
      totals.engagementTotal += safeNumber(chapter.engagementRatePercent);
      totals.quizTotal += safeNumber(chapter.quizScoreRatePercent);
      totals.dialogueSkipTotal += safeNumber(chapter.dialogueSkipRatePercent);
      totals.artifactDiscoveryTotal += safeNumber(
        chapter.artifactDiscoveryRatePercent
      );
      totals.playTimeTotal += safeNumber(chapter.playTimeSeconds);
    }
  }

  const chapters = Array.from(chapterTotals.values())
    .map((totals) => ({
      chapterId: totals.chapterId,
      participantCount: totals.participantCount,
      averageEngagementRatePercent: average(
        totals.engagementTotal,
        totals.participantCount
      ),
      averageQuizScoreRatePercent: average(
        totals.quizTotal,
        totals.participantCount
      ),
      averageDialogueSkipRatePercent: average(
        totals.dialogueSkipTotal,
        totals.participantCount
      ),
      averageArtifactDiscoveryRatePercent: average(
        totals.artifactDiscoveryTotal,
        totals.participantCount
      ),
      averagePlayTimeSeconds: average(
        totals.playTimeTotal,
        totals.participantCount
      )
    }))
    .sort((left, right) => left.chapterId.localeCompare(right.chapterId));

  logger.info("Loaded aggregated Global Analytics.", {
    librarianPlayerId: context.playerId,
    participantCount,
    chapterCount: chapters.length
  });

  return {
    participantCount,
    chapters
  };
};

async function loadProtectedValue(cloudSave, projectId, playerId, key) {
  const response = await cloudSave.getProtectedItems(projectId, playerId, [key]);
  return response.data.results.length > 0
    ? response.data.results[0].value
    : null;
}

function safeNumber(value) {
  return Number.isFinite(value) && value >= 0 ? value : 0;
}

function average(total, count) {
  return count > 0 ? total / count : 0;
}
