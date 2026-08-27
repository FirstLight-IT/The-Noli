const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY = "account_profile";
const OWNED_ROOMS_KEY = "teacher_owned_classroom_ids";
const ROOM_KEY = "room";

module.exports = async ({ context, logger }) => {
  const cloudSave = new DataApi(context);
  const profile = await loadProtectedValue(
    cloudSave,
    context.projectId,
    context.playerId,
    PROFILE_KEY
  );

  if (!profile ||
      profile.role !== "Teacher" ||
      profile.teacherVerificationStatus !== "Verified") {
    throw new Error("Only a verified Teacher may manage classrooms.");
  }

  const ownedRoomIds = await loadProtectedValue(
    cloudSave,
    context.projectId,
    context.playerId,
    OWNED_ROOMS_KEY
  );
  if (!Array.isArray(ownedRoomIds) || ownedRoomIds.length === 0) {
    return { rooms: [] };
  }

  const rooms = [];
  for (const roomId of ownedRoomIds) {
    const response = await cloudSave.getPrivateCustomItems(
      context.projectId,
      roomEntityId(roomId)
    );
    const item = response.data.results.find(result => result.key === ROOM_KEY);
    const room = item ? item.value : null;
    if (!room || room.teacherAccountId !== context.playerId) {
      continue;
    }
    if (room.status === "Deleted" || room.status === "Archived") {
      continue;
    }

    rooms.push({
      roomId: room.roomId,
      roomName: room.roomName,
      joinCode: room.joinCode,
      status: room.status,
      createdAtUtc: room.createdAtUtc,
      memberCount: Array.isArray(room.members)
        ? room.members.filter(member => member && member.status === "Active").length
        : 0
    });
  }

  rooms.sort((left, right) =>
    String(right.createdAtUtc).localeCompare(String(left.createdAtUtc))
  );

  logger.info("Listed Teacher classrooms.", {
    teacherAccountId: context.playerId,
    roomCount: rooms.length
  });

  return { rooms };
};

async function loadProtectedValue(cloudSave, projectId, playerId, key) {
  const response = await cloudSave.getProtectedItems(projectId, playerId, [key]);
  const item = response.data.results.find(result => result.key === key);
  return item ? item.value : null;
}

function roomEntityId(roomId) {
  return `classroom_${roomId}`;
}

module.exports.params = {};
