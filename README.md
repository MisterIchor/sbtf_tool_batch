# sbtftool

Unofficial tool for extracting and repackaging game assets from [Space Beast Terror Fright](https://store.steampowered.com/app/357330/Space_Beast_Terror_Fright/).

## Usage

Extract the game files into the folder `output` in the working directory: 

```.\sbtftool.exe unpack "C:\Program Files (x86)\Steam\steamapps\common\Space Beast Terror Fright\sbtf_pub.nwf"```

Generate a schema file (`schema.xml`) for repackaging:

```.\sbtftool.exe schema "C:\Program Files (x86)\Steam\steamapps\common\Space Beast Terror Fright\sbtf_pub.nwf"```

Repackage the game assets in the `output` folder into `sbtf_pub.nwf` in the working directory:

```.\sbtftool.exe repack schema.xml output sbtf_pub.nwf```

The assets in the `output` folder can be edited. It should be possible to modify sound, texture and some data files. Models and some other files use a proprietary format that is not yet reverse engineered.

# License

[This project is licensed under the CC0](LICENSE)
