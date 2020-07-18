# UsmToolkit

Tool to convert USM video files into user-friendly formats.

## Getting started

Download the latest version and run `UsmToolkit get-dependencies`. This will download ffmpeg and vgmstream from the URLs provided in `deps.json`. These are neccessary for this tool to operate!

After that, it's as easy as it can get.

### Extracting
```
UsmToolkit extract <file/folder>
```

### Joining (aka converting)
```
UsmToolkit extract <file/folder> --join
```

For more informations run `UsmToolkit extract -h`.

## Custom join parameter

You should find `config.json` in the folder of the executable. With it, you can completly customize how the extracted file is processed by ffmpeg.
The default configuration ships as follows:

* Video: Will be copied
* Audio: Re-encoded as AC3 at 640kb/s. If the file has 6 channels, they will be merged to 2
* Output is a MP4 file

You can change these settings to your likings, it's standard ffmpeg syntax.

## License

UsmToolkit follows the MIT License. It uses code from [VGMToolbox](https://sourceforge.net/projects/vgmtoolbox/).