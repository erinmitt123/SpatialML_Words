// Word World detection packet: PICO yolo-mesh's live ray packet plus the stereo depth from its
// original packing script (both from source B; see docs/porting-notes.md).
// Row layout (20 floats): score, class, box width and height on the LEFT camera's one-metre plane,
// then a row-major 4x4 HEAD-local pose whose translation is the box centre on that plane.
// Lane 16 (the pose's bottom-left cell, always 0 in a rigid transform) carries the stereo axial
// depth in metres, or 0 when the five central samples do not agree. The app prefers the room mesh
// and falls back to this depth when the mesh has no surface there.
var candidate_boxes;
var candidate_classes;
var nms_indices;
var nms_scores;
var point_xyz;
var vst_camera_matrix;
var left_eye_transform;
var detections;
var frame_counter;
var next_counter;
var vst_timestamp;
for (var m = 0; m < 660; m++) detections[m] = 0;
next_counter[0] = frame_counter[0] + 1;
detections[640] = next_counter[0];
for (var k = 0; k < 16; k++) detections[641 + k] = left_eye_transform[k];
detections[657] = vst_timestamp[1];
detections[658] = Math.floor(vst_timestamp[3] / 1000000);
detections[659] = vst_timestamp[3] % 1000000;
for (var i = 0; i < 32; i++) {
    var index = nms_indices[i], score = nms_scores[i];
    if (index < 0 || index >= 128 || score < 0.5) continue;
    var b = index * 4, out = i * 20;
    var x1 = Math.max(0, Math.min(639, candidate_boxes[b]));
    var y1 = Math.max(0, Math.min(639, candidate_boxes[b + 1]));
    var x2 = Math.max(0, Math.min(639, candidate_boxes[b + 2]));
    var y2 = Math.max(0, Math.min(639, candidate_boxes[b + 3]));
    var fx = vst_camera_matrix[0], fy = vst_camera_matrix[4];
    if (fx <= 0 || fy <= 0 || !isFinite(x1 + y1 + x2 + y2 + fx + fy) || x2 <= x1 || y2 <= y1) continue;
    var x = ((x1 + x2) * 0.5 - vst_camera_matrix[2]) / fx;
    var y = -((y1 + y2) * 0.5 - vst_camera_matrix[5]) / fy;
    detections[out] = score;
    detections[out + 1] = candidate_classes[index];
    detections[out + 2] = (x2 - x1) / fx;
    detections[out + 3] = (y2 - y1) / fy;
    for (var r = 0; r < 4; r++) {
        for (var c = 0; c < 3; c++) detections[out + 4 + r * 4 + c] = left_eye_transform[r * 4 + c];
        detections[out + 7 + r * 4] = left_eye_transform[r * 4] * x +
            left_eye_transform[r * 4 + 1] * y - left_eye_transform[r * 4 + 2] + left_eye_transform[r * 4 + 3];
    }
    // Stereo depth: a hole or another object's edge must not own the depth, so require 3 of the
    // 5 central samples to agree with their median.
    var depths = [];
    for (var s = 0; s < 5; s++) {
        var measured = -point_xyz[(i * 5 + s) * 3 + 2];
        if (isFinite(measured) && measured >= 0.2 && measured <= 10) depths.push(measured);
    }
    if (depths.length < 3) continue;
    depths.sort(function (a, b) { return a - b; });
    var depth = depths[Math.floor(depths.length / 2)];
    var tolerance = Math.max(0.1, depth * 0.05), supporting = 0;
    for (var d = 0; d < depths.length; d++) if (Math.abs(depths[d] - depth) <= tolerance) supporting++;
    if (supporting >= 3) detections[out + 16] = depth;
}
