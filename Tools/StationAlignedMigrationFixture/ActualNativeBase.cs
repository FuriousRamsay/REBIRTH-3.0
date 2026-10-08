using System.IO;namespace NativeLifecycle {public partial class TileBase {
public virtual void read(PooledBinaryReader _br, StreamModeRead _eStreamMode)
	{
		if (_eStreamMode == StreamModeRead.Persistency)
		{
			readVersion = _br.ReadUInt16();
			chunkPos = StreamUtils.ReadVector3i(_br);
			if (readVersion <= 18)
			{
				_br.ReadInt32();
			}
			if (readVersion > 1)
			{
				heapMapUpdateTime = _br.ReadUInt64();
				heapMapLastTime = heapMapUpdateTime - AIDirector.GetActivityWorldTimeDelay();
			}
		}
		else
		{
			chunkPos = StreamUtils.ReadVector3i(_br);
		}
	}
public void ReadNativeBase(PooledBinaryReader _br, StreamModeRead _eStreamMode)
	{
		if (_eStreamMode == StreamModeRead.Persistency)
		{
			readVersion = _br.ReadUInt16();
			chunkPos = StreamUtils.ReadVector3i(_br);
			if (readVersion <= 18)
			{
				_br.ReadInt32();
			}
			if (readVersion > 1)
			{
				heapMapUpdateTime = _br.ReadUInt64();
				heapMapLastTime = heapMapUpdateTime - AIDirector.GetActivityWorldTimeDelay();
			}
		}
		else
		{
			chunkPos = StreamUtils.ReadVector3i(_br);
		}
	}
public virtual void write(PooledBinaryWriter _bw, StreamModeWrite _eStreamMode)
	{
		if (_eStreamMode == StreamModeWrite.Persistency)
		{
			_bw.Write((ushort)19);
			StreamUtils.Write(_bw, chunkPos);
			_bw.Write(heapMapUpdateTime);
		}
		else
		{
			StreamUtils.Write(_bw, chunkPos);
		}
	}
public virtual void SetHandle(byte _handle)
	{
		if (lockHandleWaitingFor != byte.MaxValue && lockHandleWaitingFor == _handle)
		{
			lockHandleWaitingFor = byte.MaxValue;
		}
	}
}public static class StreamUtils {
public static Vector3i ReadVector3i(BinaryReader _br)
	{
		return new Vector3i(_br.ReadInt32(), _br.ReadInt32(), _br.ReadInt32());
	}
public static void Write(BinaryWriter _bw, Vector3i _v)
	{
		_bw.Write(_v.x);
		_bw.Write(_v.y);
		_bw.Write(_v.z);
	}
} }