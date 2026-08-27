const { DataApi } = require("@unity-services/cloud-save-1.4");

const MEMBERSHIPS_KEY = "classroom_memberships";
const ROOM_KEY = "room";

module.exports = async ({ params, context, logger }) => {
  const roomId = clean(params.roomId);
  if (!roomId) {
    return failed("A classroom ID is required.");
  }

  const cloudSave = new DataApi(context);
  const savedMemberships = await loadProtectedValue(
    cloudSave, context.projectId, context.playerId, MEMBERSHIPS_KEY
  );
  const memberships = Array.isArray(savedMemberships) ? savedMemberships : [];
  const membership = memberships.find(item => item && item.roomId === roomId);
  if (!membership || membership.status !== "Active") {
    return failed("You are not an active member of this classroom.");
  }

  const room = await loadPrivateCustomValue(
    cloudSave, context.projectId, roomEntityId(roomId), ROOM_KEY
  );
  if (!room) {
    return failed("The classroom could not be found.");
  }

  const now = new Date().toISOString();
  membership.status = "Inactive";
  membership.leftAtUtc = now;

  room.members = Array.isArray(room.members) ? room.members : [];
  const member = room.members.find(item => item.accountId === context.playerId);
  if (member) {
    member.status = "Inactive";
    member.leftAtUtc = now;
  }

  await cloudSave.setProtectedItem(context.projectId, context.playerId, {
    key: MEMBERSHIPS_KEY,
    value: memberships
  });
  await cloudSave.setPrivateCustomItem(context.projectId, roomEntityId(roomId), {
    key: ROOM_KEY,
    value: room
  });

  logger.info("Player left a classroom.", {
    roomId,
    playerId: context.playerId
  });
  return { success: true, error: "" };
};

function failed(error) {
  return { success: false, error };
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

function roomEntityId(roomId) {
  return `classroom_${roomId}`;
}

function clean(value) {
  return typeof value === "string" ? value.trim() : "";
}

module.exports.params = {
  roomId: { type: "String", required: true }
};
