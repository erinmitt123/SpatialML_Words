# tools/spatialml

`build_yolo_package.js` turns PICO's yolo-mesh sample pipeline into a Unity Pipeline Zoo package.

```sh
node tools/spatialml/build_yolo_package.js <yolo-mesh-sample-root> <out-dir> [--target gpu|npu|cpu] [--rays-only]
```

Then import it (Unity closed):

```sh
Unity -batchmode -quit -projectPath . \
  -executeMethod ByteDance.PICO.SecureMR.Editor.SpatialMLPipelineZooImporterCli.Import \
  -spatialMLPackageSource <out-dir> \
  -spatialMLDestinationRoot Assets/SpatialMLPipelineZoo \
  -spatialMLOverwritePolicy Replace
```

The script defaults to `gpu`, which the checked-in package uses. If Swan logs `LiteRt create input buffer failed`, reboot the headset (see `docs/porting-notes.md`).

By default the package carries stereo depth as well as rays (`live-hybrid-output.js`); `--rays-only` uses the sample's mesh-only script.

The generated `*.tflite.bytes` models are git-ignored (PICO permission is verbal only so far). After cloning, run the two commands above to restore them; their `.meta` files are committed so scene references keep working.
