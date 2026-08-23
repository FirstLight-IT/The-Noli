const { DataApi } = require("@unity-services/cloud-save-1.4");

const SUBMISSION_KEY = "global_analytics_submission";

module.exports = async ({ context }) => {
  const cloudSave = new DataApi(context);
  const response = await cloudSave.getProtectedItems(
    context.projectId,
    context.playerId,
    [SUBMISSION_KEY]
  );

  if (response.data.results.length === 0) {
    return {
      hasOfficialPlaythrough: false,
      officialPlaythroughId: "",
      acceptedChapterIds: []
    };
  }

  const submission = response.data.results[0].value;
  const acceptedChapterIds = Array.isArray(submission.chapters)
    ? submission.chapters
        .filter((chapter) => chapter && typeof chapter.chapterId === "string")
        .map((chapter) => chapter.chapterId)
        .sort()
    : [];

  return {
    hasOfficialPlaythrough: true,
    officialPlaythroughId: submission.playthroughId || "",
    acceptedChapterIds
  };
};
