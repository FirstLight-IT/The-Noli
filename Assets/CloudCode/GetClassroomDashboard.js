const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY = "account_profile";
const ROOM_KEY = "room";
const EXPECTED_SCHEMA_VERSION = 2;
const MAX_MEMBERS = 50;

module.exports = async ({ params, context }) => {
  const roomId = clean(params.roomId);
  const cloudSave = new DataApi(context);
  const profile = await loadProtectedValue(
    cloudSave, context.projectId, context.playerId, PROFILE_KEY
  );
  if (!profile || profile.role !== "Teacher" ||
      profile.teacherVerificationStatus !== "Verified") {
    return failed("Verified Teacher access is required.");
  }

  const room = await loadPrivateCustomValue(
    cloudSave, context.projectId, roomEntityId(roomId), ROOM_KEY
  );
  if (!room || room.teacherAccountId !== context.playerId) {
    return failed("The classroom could not be found.");
  }

  const sourceMembers = Array.isArray(room.members)
    ? room.members
        .filter(member => member && member.status === "Active")
        .slice(0, MAX_MEMBERS)
    : [];
  const loadedMembers = await Promise.all(sourceMembers.map(async member => {
    let progress = null;
    try {
      progress = await loadProtectedValue(
        cloudSave,
        context.projectId,
        member.accountId,
        progressKey(roomId)
      );
      if (!isValidProgress(progress, roomId, member.accountId)) {
        progress = null;
      }
    } catch (_) {
      progress = null;
    }
    return { member, progress };
  }));

  const chapterTotals = new Map();
  let participantCount = 0;
  const members = loadedMembers.map(({ member, progress }) => {
    const chapters = progress && Array.isArray(progress.chapters)
      ? progress.chapters
      : [];
    if (chapters.length > 0) {
      participantCount++;
      for (const chapter of chapters) {
        addChapter(chapterTotals, chapter);
      }
    }

    return {
      accountId: member.accountId || "",
      inGameName: member.inGameName || "Unknown Player",
      hasUploadedAnalytics: chapters.length > 0,
      uploadedChapterCount: chapters.length,
      lastUploadedAtUtc: progress ? clean(progress.lastSyncedAtUtc) : "",
      uploadedChapterIds: chapters
        .map(chapter => clean(chapter.chapterId))
        .filter(Boolean)
        .sort()
    };
  });

  members.sort((left, right) => left.inGameName.localeCompare(right.inGameName));
  const chapters = Array.from(chapterTotals.values())
    .map(totals => ({
      chapterId: totals.chapterId,
      participantCount: totals.participantCount,
      averageEngagementRatePercent: average(
        totals.engagementTotal, totals.participantCount
      ),
      averageQuizScoreRatePercent: average(
        totals.quizTotal, totals.participantCount
      ),
      averageDialogueSkipRatePercent: average(
        totals.dialogueSkipTotal, totals.participantCount
      ),
      averageArtifactDiscoveryRatePercent: average(
        totals.artifactDiscoveryTotal, totals.participantCount
      ),
      averagePlayTimeSeconds: average(
        totals.playTimeTotal, totals.participantCount
      )
    }))
    .sort((left, right) => left.chapterId.localeCompare(right.chapterId));

  return {
    success: true,
    error: "",
    roomId,
    roomName: room.roomName || "Classroom",
    joinCode: room.joinCode || "",
    status: room.status || "Unknown",
    participantCount,
    members,
    chapters
  };
};

function isValidProgress(progress, roomId, accountId) {
  return progress &&
    progress.schemaVersion === EXPECTED_SCHEMA_VERSION &&
    clean(progress.roomId) === roomId &&
    clean(progress.accountId) === accountId &&
    Array.isArray(progress.chapters);
}

function addChapter(chapterTotals, chapter) {
  if (!chapter || !clean(chapter.chapterId) ||
      chapter.hasEngagementScore !== true) {
    return;
  }

  const chapterId = clean(chapter.chapterId);
  let totals = chapterTotals.get(chapterId);
  if (!totals) {
    totals = {
      chapterId,
      participantCount: 0,
      engagementTotal: 0,
      quizTotal: 0,
      dialogueSkipTotal: 0,
      artifactDiscoveryTotal: 0,
      playTimeTotal: 0
    };
    chapterTotals.set(chapterId, totals);
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

function safeNumber(value) {
  return Number.isFinite(value) && value >= 0 ? value : 0;
}
function average(total, count) { return count > 0 ? total / count : 0; }
function clean(value) { return typeof value === "string" ? value.trim() : ""; }
function roomEntityId(roomId) { return `classroom_${roomId}`; }
function progressKey(roomId) { return `classroom_progress_${roomId}`; }

function failed(error) {
  return {
    success: false,
    error,
    roomId: "",
    roomName: "",
    joinCode: "",
    status: "",
    participantCount: 0,
    members: [],
    chapters: []
  };
}

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

module.exports.params = { roomId: { type: "String", required: true } };
