using System;
using System.Text;
using static MaGeneralUtilities.GeneralUtilities.GeneralUtilities; //Requires v0.1

namespace TestCommData
{
    //Data definition library
    public enum BlockStatus
    {
        Empty,
        ReceivedFromClient,
        ReceivedFromSimConnect,
        SentToClient,
        SentToSimConnect
    }
    public enum Acknowledge
    {
        CommunicationOK,
        CommunicationFailed,
        Data2send,
        NoData2send,
        Ready2receive
    }
    //DataDefinition contains all the constants that define
    //the data and datablocks
    public static class DataDefinition
    {
        //Set encoding standard
        public static readonly Encoding encoding = Encoding.UTF8;

        //BlockSize and number of blocks
        public const int maxBlocks = 1;
        public const int blockSize = 256;

        //FixedDataElements contains the definitions of the blocknumber, the filler and checksum
        public static class FixedDataElements
        {
            //Elements in every block
            //BlockNumber (Int32)
            public const int blockNumberOffset = 0x0000;
            public const int blockNumberLength = 4;
            //Checksum (Uint64)
            public const int checkSumLength = 8;
            public static readonly int checkSumOffset = blockSize - checkSumLength;

            //Add fixed elements to a block
            //Packs blocknumber, filler and checksum to the specified block
            public static void BuildBlockFixed(byte[] target, int blocknum)
            {
                //Add blocknumber to uitvoer
                PackNumber(blocknum, target, blockNumberOffset);

                //Add filler to uitvoer
                PackByteArray(Filler(blocknum), target, FillerOffset(blocknum));

                //Add checksum to uitvoer
                byte[] werk = new byte[target.Length - 8];
                Array.Copy(target, 0, werk, 0, werk.Length);
                UInt64 checksum = GetChecksum(werk, 64);
                PackNumber(checksum, target, checkSumOffset);

            }

            //Extractions
            public static Int32 BlockNumber(byte[] block)
            {
                return ExtractInt32(block, blockNumberOffset);
            }
            public static UInt64 CheckSum(byte[] block)
            {
                return ExtractUInt64(block, checkSumOffset);
            }

            //Filler
            private static byte[] Filler(int blocknum)
            {
                byte[] uitvoer = new byte[1];
                switch (blocknum)
                {
                    case 0:
                        uitvoer = new byte[Block0DataElements.fillerBlock0lentgh];
                        break;
                }

                return uitvoer;
            }

            //returns the offset of the filler in the specified block
            private static int FillerOffset(int blocknum)
            {
                int uitvoer = 0;
                switch (blocknum)
                {
                    case 0:
                        uitvoer = Block0DataElements.fillerBlock0offset;
                        break;
                }
                return uitvoer;
            }


        }

        public static class Block0DataElements
        {
            //Elements in Block 0
            //FirstMessage (string, lenght = 10)
            public const int firstMessageBlocknumber = 0;
            public const int firstMessageOffset = 0x0004;
            public const int firstMessageLength = 10;

            //FirstNumber (Int32)
            public const int firstNumberBlocknumber = 0;
            public const int firstNumberOffset = 0x000E;
            public const int firstNumberLength = 4;

            //Filler ((byte)0 * lentgh)
            public const int fillerBlock0Blocknumber = 0;
            private static readonly int block0datasize = FixedDataElements.blockNumberLength + firstMessageLength + firstNumberLength;
            public static readonly int fillerBlock0offset = block0datasize;
            public static readonly int fillerBlock0lentgh = blockSize - (block0datasize + FixedDataElements.checkSumLength);

            //build Block 0
            //Overloaded for every block
            public static byte[] BuildBlock(string firstMessage, Int32 firstNumber)
            {
                byte[] uitvoer = new byte[blockSize];
                Int32 blockNumber = (Int32)0;

                //Add firstMessage to uitvoer
                PackString(firstMessage, uitvoer, firstMessageOffset, firstMessageLength);

                //Add firstNumber to uitvoer
                PackNumber(firstNumber, uitvoer, firstNumberOffset);

                //Add blocknumber, filler and checksum
                FixedDataElements.BuildBlockFixed(uitvoer, blockNumber);

                return uitvoer;
            }

            //Extractions
            public static string FirstMessage(byte[] block)
            {
                return ExtractString(block, firstMessageOffset, firstMessageLength);
            }

            public static Int32 FirstNumber(byte[] block)
            {
                return ExtractInt32(block, firstNumberOffset);
            }

        }

        //Utilities
        //++++++++++++++++++++++++++++++++++++++
        //Packing utilities


        //Packs a number to the specified place in the array
        private static void PackNumber(Int32 Number, byte[] target, int offset)
        {
            Array.Copy(EndianRightArrange(BitConverter.GetBytes(Number)), 0, target, offset, EndianRightArrange(BitConverter.GetBytes(Number)).Length);
        }
        private static void PackNumber(UInt64 Number, byte[] target, int offset)
        {
            Array.Copy(EndianRightArrange(BitConverter.GetBytes(Number)), 0, target, offset, EndianRightArrange(BitConverter.GetBytes(Number)).Length);
        }

        //Packs a string to the specified place in the array
        private static void PackString(string tekst, byte[] target, int offset, int length)
        {
            Array.Copy(encoding.GetBytes(tekst.PadRight(length, '\0')), 0, target, offset, length);
        }

        //packs a byte[] to the specified place in the array
        private static void PackByteArray(byte[] bytes, byte[] target, int offset)
        {
            Array.Copy(bytes, 0, target, offset, bytes.Length);
        }
        //++++++++++++++++++++++++++++++++++++++++++++++++++++++++

        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        //Extraction utilites
        //Extracts a string out of a block
        private static string ExtractString(byte[] block, int offset, int length)
        {
            return encoding.GetString(block, offset, length);
        }

        //Extracts a Int32 out of a block
        public static Int32 ExtractInt32(byte[] block, int offset)
        {
            int length = 4;

            return BitConverter.ToInt32(ExtractNum(block, offset, length));
        }
        public static UInt64 ExtractUInt64(byte[] block, int offset)
        {
            int length = 8;

            return BitConverter.ToUInt64(ExtractNum(block, offset, length));
        }

        //Extracts a byte[] of specified lenth out of a block to be converted to a number
        private static byte[] ExtractNum(byte[] block, int offset, int length)
        {
            byte[] werk = new byte[length];

            Array.Copy(block, offset, werk, 0, length);

            return EndianRightArrange(werk);
        }
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++

    }

    public class DataBlockEventArgs : EventArgs
    {
        public int BlockIndex { get; set; }
    }

    public class DataBlock
    {

        public byte[] Data { get; set; } = new byte[DataDefinition.blockSize];
        public BlockStatus Status { get; set; } = BlockStatus.Empty;
        public bool IsLocked { get; set; } = false;
    }

    public class DataStorage
    {
        private static readonly DataBlock[] dataBlocks = new DataBlock[DataDefinition.maxBlocks];
        public int currentBlockIndex = 0; // Index of the current block in use

        public event EventHandler<DataBlockEventArgs>? BlockEmptied;
        public event EventHandler<DataBlockEventArgs>? BlockReleased;

        public DataStorage()
        {
            for (int i = 0; i < DataDefinition.maxBlocks; i++)
            {
                dataBlocks[i] = new DataBlock(); // Initialize with new instances
            }
        }

        public DataBlock GetCurrentBlock() => dataBlocks[currentBlockIndex];
        public static DataBlock GetBlock(int index) => dataBlocks[index];

        public void Initialize()
        {
            for (int i = 0; i < DataDefinition.maxBlocks; i++)
            {
                SetBlockStatus(i, BlockStatus.Empty);
            }
        }
        public void AdvanceToNextBlock()
        {
            currentBlockIndex = (currentBlockIndex + 1) % DataDefinition.maxBlocks;
        }

        //sets the blockstatus and writes the block
        public void SetBlockStatus(int blocknum, BlockStatus status, byte[] block)
        {
            //Only execute if block is valid, otherwise disregard
            if (BlockIsValid(block))
            {
                dataBlocks[blocknum].Data = block; //commit data
                SetBlockStatus(blocknum, status);
            }
        }

        //sets the blockstatus of an existing block
        public void SetBlockStatus(int blocknum, BlockStatus status)
        {
            dataBlocks[blocknum].Status = status;
            if (status == BlockStatus.Empty)
            {
                BlockEmptied?.Invoke(this, new DataBlockEventArgs { BlockIndex = blocknum });
            }

        }

        public static void BlockLock(int blocknum)
        {
            dataBlocks[blocknum].IsLocked = true;
        }

        public void BlockUnlock(int blocknum)
        {
            dataBlocks[blocknum].IsLocked = false;
            BlockReleased?.Invoke(this, new DataBlockEventArgs { BlockIndex = blocknum });
        }

        public static bool BlockIsValid(byte[] block)
        {
            bool Uitvoer = true;

            if (block == null || block.Length != DataDefinition.blockSize)
            {
                Uitvoer = false;
            }
            else
            {
                byte[] werk1 = new byte[block.Length - 8];
                UInt64 checksumFound = DataDefinition.FixedDataElements.CheckSum(block);
                Array.Copy(block, 0, werk1, 0, werk1.Length);
                UInt64 checksumCalculated = GetChecksum(werk1, 64);
                if (checksumFound == checksumCalculated)
                {
                    Uitvoer = true;
                }
            }
            return Uitvoer;
        }

    }


}
