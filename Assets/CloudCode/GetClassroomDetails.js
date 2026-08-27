const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY = "account_profile";
const ROOM_KEY = "room";

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

  const members = Array.isArray(room.members) ? room.members.map(member => ({
    accountId: member.accountId || "",
    inGameName: member.inGameName || "Unknown Player",
    status: member.status || "Inactive",
    joinedAtUtc: member.joinedAtUtc || "",
    leftAtUtc: member.leftAtUtc || ""
  })) : [];

  members.sort((left, right) => {
    if (left.status !== right.status) return left.status === "Active" ? -1 : 1;
    return left.inGameName.localeCompare(right.inGameName);
  });

  return { success: true, error: "", members };
};

function failed(error) { return { success: false, error, members: [] }; }
function roomEntityId(roomId) { return `classroom_${roomId}`; }
function clean(value) { return typeof value === "string" ? value.trim() : ""; }

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
