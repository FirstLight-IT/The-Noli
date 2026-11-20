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

  const statuses = [
    "Pending",
    "AwaitingEmailConfirmation",
    "Verified",
    "Rejected"
  ];
  const resultByAccountId = new Map();
  for (const status of statuses) {
    const results = await queryByStatus(cloudSave, context.projectId, status);
    for (const result of results) {
      const accountId = result.id || result.playerId;
      if (accountId) {
        resultByAccountId.set(accountId, result);
      }
    }
  }

  const results = Array.from(resultByAccountId.values());
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
      typeof profile.teacherVerificationStatus === "string"
    ) {
      requests.push({
        accountId: targetAccountId,
        username: profile.username,
        inGameName: profile.inGameName,
        fullName: profile.fullName,
        schoolEmail: profile.schoolEmail,
        teacherVerificationStatus: profile.teacherVerificationStatus,
        submittedAtUtc: profile.createdAtUtc || ""
      });
    }
  }

  requests.sort((left, right) =>
    String(right.submittedAtUtc).localeCompare(String(left.submittedAtUtc))
  );
  return { requests };
};

async function queryByStatus(cloudSave, projectId, status) {
  const query = {
    fields: [
      {
        asc: true,
        key: TEACHER_REQUEST_STATUS_KEY,
        op: "EQ",
        value: status
      }
    ],
    returnKeys: [TEACHER_REQUEST_STATUS_KEY],
    limit: 100
  };

  const response = await cloudSave.queryProtectedPlayerData(projectId, query);
  return response.data.results || [];
}

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
