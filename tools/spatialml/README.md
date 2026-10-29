# tools/spatialml

`build_yolo_package.js` turns PICO's yolo-mesh sample pipeline into a Unity Pipeline Zoo package.

```sh
node tools/spatialml/build_yolo_package.js <yolo-mesh-sample-root> <out-dir> [--target gpu|npu]
```

Then import it (Unity closed):

```sh
Unity -batchmode -quit -projectPath . \
  -executeMethod ByteDance.PICO.SecureMR.Editor.SpatialMLPipelineZooImporterCli.Import \
  -spatialMLPackageSource <out-dir> \
  -spatialMLDestinationRoot Assets/SpatialMLPipelineZoo \
  -spatialMLOverwritePolicy Replace
```

The script defaults to `gpu`, but on Swan both GPU and NPU currently fail for YOLOX-M; the checked-in package uses `--target cpu` for testing only. See `docs/porting-notes.md`.

The generated `*.tflite.bytes` models are git-ignored (PICO permission is verbal only so far). After cloning, run the two commands above to restore them; their `.meta` files are committed so scene references keep working.
