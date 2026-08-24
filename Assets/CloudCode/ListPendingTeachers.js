const { DataApi } = require("@unity-services/cloud-save-1.4");

// This endpoint has no client parameters.

const PROFILE_KEY = "account_profile";
const TEACHER_REQUEST_STATUS_KEY = "teacher_request_status";

module.exports = async ({ context }) => {
  const cloudSave = new DataApi(context);
  const librarianProfile = await loadProfile(
    cloudSave,
    context.projectId,
    context.playerId
  );

  if (
    !librarianProfile ||
    librarianProfile.role !== "Librarian" ||
    librarianProfile.effectiveRole !== "Librarian"
  ) {
    throw new Error("Only a verified Librarian may view Teacher requests.");
  }

  const query = {
    fields: [
      {
        asc: true,
        key: TEACHER_REQUEST_STATUS_KEY,
        op: "EQ",
        value: "Pending"
      }
    ],
    returnKeys: [TEACHER_REQUEST_STATUS_KEY],
    limit: 100
  };

  const response = await cloudSave.queryProtectedPlayerData(
    context.projectId,
    query
  );
  const results = response.data.results || [];
  const requests = [];

  for (const result of results) {
    const targetAccountId = result.id || result.playerId;
    if (!targetAccountId) {
      continue;
    }

    const profile = await loadProfile(
      cloudSave,
      context.projectId,
      targetAccountId
    );

    if (
      profile &&
      profile.role === "Teacher" &&
      profile.teacherVerificationStatus === "Pending"
    ) {
      requests.push({
        accountId: targetAccountId,
        username: profile.username,
        inGameName: profile.inGameName,
        fullName: profile.fullName,
        schoolEmail: profile.schoolEmail,
        teacherVerificationStatus: profile.teacherVerificationStatus
      });
    }
  }

  return { requests };
};

async function loadProfile(cloudSave, projectId, playerId) {
  const response = await cloudSave.getProtectedItems(
    projectId,
    playerId,
    [PROFILE_KEY]
  );

  return response.data.results.length > 0
    ? response.data.results[0].value
    : null;
}
