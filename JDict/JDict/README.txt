
Readme

--------------------
Database file format
--------------------

* All integers are little endian
* String: uint16 size of string followed by utf-8 encoding

Order:

1. File header
2. Dictionary entries
3. Search index



File header
-----------

1. 4 bytes ascii: TDIC
2. uint32: Offset to search index from beginning of file
3. uint32: Search index count (number of entries)


Dictionary entry
----------------

1. uint16: Count of pairs

pairs:
1. string: metadata
2. string: data


Search index
------------

1. string: index entry string
3. uint8: count of xref entries
4. repeating #3 times: 4 byte unsigned integer file offset of dictionary entry from beginning of file
