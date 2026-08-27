const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY = "account_profile";
const ROOM_KEY = "room";

module.exports = async ({ params, context, logger }) => {
  const roomId = clean(params.roomId);
  const requestedStatus = clean(params.status);
  if (requestedStatus !== "Deleted") {
    return failed("The classroom status must be Deleted.");
  }

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

  room.status = requestedStatus;
  room.deletedAtUtc = new Date().toISOString();
  room.statusChangedAtUtc = new Date().toISOString();
  await cloudSave.setPrivateCustomItem(context.projectId, roomEntityId(roomId), {
    key: ROOM_KEY,
    value: room
  });
  logger.info("Classroom status changed.", { roomId, status: requestedStatus });
  return { success: true, error: "", status: requestedStatus };
};

function failed(error) { return { success: false, error, status: "" }; }
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

module.exports.params = {
  roomId: { type: "String", required: true },
  status: { type: "String", required: true }
};
