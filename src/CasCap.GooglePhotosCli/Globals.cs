using System;

[Flags]
public enum GroupByProperty
{
    filename = 1,
    mimeType = 2,
    dimensions = 4,
    creationTime = 8,
    description = 16,
    focalLength = 32,//photo
    apertureFNumber = 64,//photo
    isoEquivalent = 128,//photo
    exposureTime = 256,//photo
    fps = 512,//video
    status = 1024, //video
    cameraMake = 2048,//photo & video
    cameraModel = 4096,//photo & video
    albumIds = 8192,
    contentCategoryTypes = 16384,
}

[Flags]
public enum MediaType
{
    All = Photo | Video,
    Photo = 2,
    Video = 4
}