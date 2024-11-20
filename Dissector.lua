excom_proto = Proto('custom-protocl', 'EXample COMmunication protocol')

-- Helper function for ProtoField names
local function field(field_name)
    return string.format('%s.%s', excom_proto.name, field_name)
end

-- RequestType enum
local request_type = {
    REQ_DISPLAY = 1,
    REQ_LED = 2,
}
-- Mapping of RequestType value to name
local request_type_names = {}
for name, value in pairs(request_type) do
    request_type_names[value] = name
end

-- Define field types available in our protocol, as a table to easily reference them later
local fields = {
    sequence_number = ProtoField.uint32(field('seq'), 'Sequence Number', base.DEC),
    -- request_t
    id = ProtoField.uint16(field('id'), 'Id', base.HEX),
    -- response_t
    flags = ProtoField.uint16(field('flags'), 'flags'),


    filename_offset = ProtoField.uint16(field('floffset'), 'Filename offset'),

    data = ProtoField.bytes(field('data'), 'Data'),
    checksum = ProtoField.uint16(field('checksum'), 'Checksum'),



}

-- Add all the types to Proto.fields list
for _, proto_field in pairs(fields) do
    table.insert(excom_proto.fields, proto_field)
end

-- Dissector callback, called for each packet
excom_proto.dissector = function(buf, pinfo, root)
    -- arguments:
    -- buf: packet's buffer (https://www.wireshark.org/docs/wsdg_html_chunked/lua_module_Tvb.html#lua_class_Tvb)
    -- pinfo: packet information (https://www.wireshark.org/docs/wsdg_html_chunked/lua_module_Pinfo.html#lua_class_Pinfo)
    -- root: node of packet details tree (https://www.wireshark.org/docs/wsdg_html_chunked/lua_module_Tree.html#lua_class_TreeItem)

    -- Set name of the protocol
    pinfo.cols.protocol:set(excom_proto.name)

    -- Add new tree node for our protocol details
    local tree = root:add(excom_proto, buf())

    -- Extract message ID, this is the same for request_t and response_t
    -- `id` is of type uint32_t, so get a sub-slice: buf(offset=0, length=4)
    local seq_num = buf(0, 2)
    tree:add_le(fields.sequence_number, seq_num)
    local id = buf(2, 2)
    tree:add_le(fields.id, id)
    local flags = buf(4, 1)
    tree:add_le(fields.flags, flags)
    local filename_offset = buf(5, 2)
    tree:add_le(fields.filename_offset, filename_offset)

    local data = buf(7, buf:len()-9)
    tree:add_le(fields.data, data)
    local checksum = buf(buf:len()-2,2)
    tree:add_le(fields.checksum, checksum)
 

end

-- Register our protocol to be automatically used for traffic on port 9000
local tcp_port1 = DissectorTable.get('udp.port')
tcp_port1:add(5050, excom_proto)


local tcp_port2 = DissectorTable.get('udp.port')
tcp_port2:add(8080, excom_proto)

local tcp_port3 = DissectorTable.get('udp.port')
tcp_port3:add(6565, excom_proto)
local tcp_port4 = DissectorTable.get('udp.port')
tcp_port4:add(5656, excom_proto)