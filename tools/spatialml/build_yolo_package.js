#!/usr/bin/env node
// Builds Word World's live YOLO SpatialML package from PICO's yolo-mesh sample (source B).
//
// Port of the sample's Gradle `syncYoloLiveAssets` task, adapted for the Unity Pipeline Zoo loader:
//   * camera access runs inside the pipeline (RECTIFIED_VST_ACCESS), so the VST tensors are
//     pipeline-local and are never exposed to the app (privacy: no camera frames in C#);
//   * the pipeline publishes one ray per detection for the app to intersect with the room mesh
//     (SPATIALML_SETUP.md section 5), plus the stereo UV-to-3D depth from the sample's original
//     graph as a fallback where the mesh has no surface (live-hybrid-output.js);
//   * the only output is the 33x20 FLOAT32 `detections` packet.
//
// Usage:
//   node tools/spatialml/build_yolo_package.js <sourceB-root> <out-dir> [--target npu|gpu|cpu] [--rays-only]
//
// --rays-only drops the stereo branch and uses the sample's live ray script unchanged.
//
// The output folder is then imported into Assets/SpatialMLPipelineZoo with the SDK importer
// (see tools/spatialml/README.md).

const fs = require('fs');
const path = require('path');

const args = process.argv.slice(2);
const positional = args.filter(a => !a.startsWith('--'));
if (positional.length < 2) {
  console.error('usage: build_yolo_package.js <sourceB-root> <out-dir> [--target npu|gpu|cpu] [--rays-only]');
  process.exit(2);
}
const targetIndex = args.indexOf('--target');
// Swan (build 20261022065912) cannot load NPU models in SSMR; GPU is the working default.
const detectorTarget = targetIndex >= 0 ? args[targetIndex + 1] : 'gpu';
if (!['npu', 'gpu', 'cpu'].includes(detectorTarget)) throw new Error(`bad --target ${detectorTarget}`);

const [sourceRoot, outDir] = positional;
const sourceApp = path.join(sourceRoot, 'yolo-mesh-sample', 'app', 'src', 'main');
const sourcePackage = path.join(sourceApp, 'assets', 'SpatialML', 'yolo-detection-pipeline');
const raysOnly = args.includes('--rays-only');
const liveScript = raysOnly
  ? fs.readFileSync(path.join(sourceApp, 'yolo', 'live-mesh-output.js'), 'utf8')
  : fs.readFileSync(path.join(__dirname, 'live-hybrid-output.js'), 'utf8');
const graph = JSON.parse(fs.readFileSync(path.join(sourcePackage, 'pipeline', 'inference.json'), 'utf8'));

const VST = ['vst_left', 'vst_right', 'vst_timestamp', 'vst_camera_matrix'];
const isType = (op, name) => op.type === `XR_SECURE_MR_OPERATOR_TYPE_${name}_PICO`;
const outputsOf = op => (op.outputs || []).map(o => (typeof o === 'string' ? o : o.tensor));

// Camera tensors become pipeline-local; the timestamp uses the loader's timestamp form.
for (const name of VST) graph.tensors[name].is_placeholder = false;
graph.tensors.vst_timestamp = { tensor_type: 'timestamp', is_placeholder: false, value: [0, 0, 0, 0] };

const pack = graph.operators.find(op => outputsOf(op).includes('detections'));
if (raysOnly) {
  // Drop the stereo-depth branch: UV2CAM and the two operators that only feed it.
  const unused = new Set(['image_points_mat', 'image_points', 'point_xyz']);
  graph.operators = graph.operators.filter(op =>
    !isType(op, 'UV_TO_3D_IN_CAM_SPACE') && !outputsOf(op).some(t => unused.has(t)));
  for (const name of unused) delete graph.tensors[name];
  pack.inputs = pack.inputs.filter(i => i.tensor !== 'point_xyz');
} else {
  if (!pack.inputs.some(i => i.tensor === 'point_xyz')) {
    throw new Error('source pack operator no longer takes point_xyz; check the hybrid script');
  }
  // The hybrid script scores rows with the model's own candidate score (see its header).
  pack.inputs.push({ name: 'candidate_scores', tensor: 'candidate_scores' });
}
pack.attrs = [liveScript];

// Loader binds camera outputs positionally: right, left, timestamp, camera matrix.
graph.operators.unshift({
  type: 'XR_SECURE_MR_OPERATOR_TYPE_RECTIFIED_VST_ACCESS_PICO',
  inputs: [],
  outputs: ['vst_right', 'vst_left', 'vst_timestamp', 'vst_camera_matrix'],
});

for (const op of graph.operators) {
  if (op.model && op.model.model_name === 'yolox_m') {
    op.model.model_target = detectorTarget;
    // CPU is a diagnostic fallback (slow); the postprocess model already uses 4 threads.
    if (detectorTarget === 'cpu') op.model.cpu_target_num_threads = 4;
  }
}

graph.inputs = [];
graph.outputs = ['detections'];

const manifest = {
  schema_version: '2',
  id: 'yolo-detection',
  pipelines: [{ id: 'inference', path: 'pipeline/inference.json' }],
  runtime: { supported_modes: ['spatial'] },
};

fs.mkdirSync(path.join(outDir, 'pipeline'), { recursive: true });
fs.mkdirSync(path.join(outDir, 'model'), { recursive: true });
fs.writeFileSync(path.join(outDir, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n');
fs.writeFileSync(path.join(outDir, 'pipeline', 'inference.json'), JSON.stringify(graph, null, 2) + '\n');
for (const model of ['yolox_m.tflite', 'yolox_postprocess.tflite']) {
  fs.copyFileSync(path.join(sourcePackage, 'model', model), path.join(outDir, 'model', model));
}

console.log(`yolo-detection package written to ${outDir} (detector target: ${detectorTarget})`);
console.log('operators:', graph.operators.map(op => op.type.replace(/^XR_SECURE_MR_OPERATOR_TYPE_|_PICO$/g, '')).join(' -> '));
