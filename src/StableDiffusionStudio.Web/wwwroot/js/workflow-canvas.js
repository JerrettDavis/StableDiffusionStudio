// Workflow Canvas — ReactFlow integration for Blazor
// Uses React/ReactFlow loaded via importmap in App.razor
// Blazor is the source of truth; ReactFlow is the visual renderer.

window.workflowCanvas = {
    _root: null,
    _dotnetRef: null,
    _container: null,

    init: function (containerId, dotnetRef, nodesJson, edgesJson) {
        this._dotnetRef = dotnetRef;
        this._container = document.getElementById(containerId);
        if (!this._container) {
            console.error('Workflow canvas container not found:', containerId);
            return;
        }

        // Load React + ReactFlow dynamically
        this._loadAndRender(nodesJson, edgesJson);
    },

    _loadAndRender: async function (nodesJson, edgesJson) {
        try {
            const React = await import('https://esm.sh/react@18.3.1');
            const ReactDOM = await import('https://esm.sh/react-dom@18.3.1/client');
            const RF = await import('https://esm.sh/@xyflow/react@12.4.4?deps=react@18.3.1,react-dom@18.3.1');

            // Inject ReactFlow CSS
            if (!document.getElementById('reactflow-css')) {
                const link = document.createElement('link');
                link.id = 'reactflow-css';
                link.rel = 'stylesheet';
                link.href = 'https://esm.sh/@xyflow/react@12.4.4/dist/style.css';
                document.head.appendChild(link);
            }

            this._React = React;
            this._RF = RF;

            const nodes = JSON.parse(nodesJson || '[]');
            const edges = JSON.parse(edgesJson || '[]');

            this._root = ReactDOM.createRoot(this._container);
            this._renderCanvas(nodes, edges);
        } catch (err) {
            console.error('Failed to load ReactFlow:', err);
            this._container.innerHTML = '<div style="padding:20px;color:var(--mud-palette-error)">Failed to load workflow canvas. Check network connectivity.</div>';
        }
    },

    _renderCanvas: function (initialNodes, initialEdges) {
        const React = this._React;
        const RF = this._RF;
        const dotnetRef = this._dotnetRef;
        const h = React.createElement;

        // Color map for node types
        const categoryColors = {
            'core.generate': '#4CAF50',
            'core.img2img': '#2196F3',
            'core.inpaint': '#9C27B0',
            'core.upscale': '#FF9800',
            'core.controlnet': '#00BCD4',
            'core.conditional': '#FFC107',
            'core.output': '#607D8B',
            'core.script': '#795548',
        };

        // Custom node component
        function WorkflowNode({ data, id }) {
            const color = categoryColors[data.pluginId] || '#666';
            const inputPorts = data.inputPorts || [];
            const outputPorts = data.outputPorts || [];

            return h('div', {
                style: {
                    background: 'var(--mud-palette-surface)',
                    border: `2px solid ${color}`,
                    borderRadius: '8px',
                    padding: '0',
                    minWidth: '180px',
                    boxShadow: '0 2px 8px rgba(0,0,0,0.15)',
                    fontSize: '13px',
                }
            },
                // Header
                h('div', {
                    style: {
                        background: color,
                        color: 'white',
                        padding: '6px 12px',
                        borderRadius: '6px 6px 0 0',
                        fontWeight: 'bold',
                        fontSize: '12px',
                        display: 'flex',
                        alignItems: 'center',
                        gap: '6px',
                    }
                }, data.label || data.pluginId),
                // Body with ports
                h('div', { style: { padding: '8px 0' } },
                    // Input handles
                    ...inputPorts.map((port, i) =>
                        h(RF.Handle, {
                            key: `in-${port.name}`,
                            type: 'target',
                            position: RF.Position.Left,
                            id: port.name,
                            style: {
                                top: `${30 + i * 24}px`,
                                background: port.required ? color : '#999',
                                width: '10px',
                                height: '10px',
                            }
                        })
                    ),
                    // Port labels
                    ...inputPorts.map((port, i) =>
                        h('div', {
                            key: `inlabel-${port.name}`,
                            style: { paddingLeft: '16px', fontSize: '11px', color: 'var(--mud-palette-text-secondary)', lineHeight: '24px' }
                        }, `${port.required ? '' : '(opt) '}${port.name}`)
                    ),
                    ...outputPorts.map((port, i) =>
                        h('div', {
                            key: `outlabel-${port.name}`,
                            style: { paddingRight: '16px', textAlign: 'right', fontSize: '11px', color: 'var(--mud-palette-text-secondary)', lineHeight: '24px' }
                        }, port.name)
                    ),
                    // Output handles
                    ...outputPorts.map((port, i) =>
                        h(RF.Handle, {
                            key: `out-${port.name}`,
                            type: 'source',
                            position: RF.Position.Right,
                            id: port.name,
                            style: {
                                top: `${30 + i * 24}px`,
                                background: color,
                                width: '10px',
                                height: '10px',
                            }
                        })
                    ),
                )
            );
        }

        const nodeTypes = { workflowNode: WorkflowNode };

        function FlowApp() {
            const [nodes, setNodes, onNodesChange] = RF.useNodesState(initialNodes);
            const [edges, setEdges, onEdgesChange] = RF.useEdgesState(initialEdges);

            // Store setter refs for external updates
            window.workflowCanvas._setNodes = setNodes;
            window.workflowCanvas._setEdges = setEdges;

            const onConnect = React.useCallback((params) => {
                setEdges((eds) => RF.addEdge({ ...params, animated: true }, eds));
                if (dotnetRef) {
                    dotnetRef.invokeMethodAsync('OnEdgeCreated',
                        params.source, params.sourceHandle || 'image',
                        params.target, params.targetHandle || 'image');
                }
            }, [setEdges]);

            const onNodeDragStop = React.useCallback((event, node) => {
                if (dotnetRef) {
                    dotnetRef.invokeMethodAsync('OnNodeMoved', node.id, node.position.x, node.position.y);
                }
            }, []);

            const onNodeClick = React.useCallback((event, node) => {
                if (dotnetRef) {
                    dotnetRef.invokeMethodAsync('OnNodeSelected', node.id);
                }
            }, []);

            const onEdgesDelete = React.useCallback((deletedEdges) => {
                deletedEdges.forEach(edge => {
                    if (dotnetRef) {
                        dotnetRef.invokeMethodAsync('OnEdgeDeleted', edge.id);
                    }
                });
            }, []);

            const onNodesDelete = React.useCallback((deletedNodes) => {
                deletedNodes.forEach(node => {
                    if (dotnetRef) {
                        dotnetRef.invokeMethodAsync('OnNodeDeleted', node.id);
                    }
                });
            }, []);

            return h(RF.ReactFlow, {
                nodes: nodes,
                edges: edges,
                onNodesChange: onNodesChange,
                onEdgesChange: onEdgesChange,
                onConnect: onConnect,
                onNodeDragStop: onNodeDragStop,
                onNodeClick: onNodeClick,
                onEdgesDelete: onEdgesDelete,
                onNodesDelete: onNodesDelete,
                nodeTypes: nodeTypes,
                fitView: true,
                deleteKeyCode: 'Delete',
                snapToGrid: true,
                snapGrid: [16, 16],
                style: { width: '100%', height: '100%' },
            },
                h(RF.Background, { variant: 'dots', gap: 16, size: 1 }),
                h(RF.Controls, null),
                h(RF.MiniMap, {
                    nodeStrokeWidth: 3,
                    style: { background: 'var(--mud-palette-surface)' }
                })
            );
        }

        this._root.render(React.createElement(FlowApp));
    },

    updateCanvas: function (nodesJson, edgesJson) {
        if (this._setNodes && this._setEdges) {
            const nodes = JSON.parse(nodesJson || '[]');
            const edges = JSON.parse(edgesJson || '[]');
            this._setNodes(nodes);
            this._setEdges(edges);
        }
    },

    highlightNode: function (nodeId, color) {
        if (this._setNodes) {
            this._setNodes(nds => nds.map(n =>
                n.id === nodeId
                    ? { ...n, style: { ...n.style, boxShadow: `0 0 12px ${color}` } }
                    : { ...n, style: { ...n.style, boxShadow: undefined } }
            ));
        }
    },

    dispose: function () {
        if (this._root) {
            this._root.unmount();
            this._root = null;
        }
        this._dotnetRef = null;
        this._setNodes = null;
        this._setEdges = null;
    }
};
